using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tts;

[Tool]
public partial class Level : Node2D
{
	private const int UnsetSeed = 0;
	private const float DragThreshold = 8f;
	private const float DoubleClickThresholdSeconds = 0.35f;

	[Export]
	public int PreviewSeed { get; set; } = UnsetSeed;

	// Clicking this checkbox in the inspector regenerates the layout.
	[Export]
	public bool Regenerate
	{
		get => false;
		set
		{
			if (value && Engine.IsEditorHint())
				GeneratePreview();
		}
	}

	private readonly Random _rng = new();
	private readonly List<SystemNode> _systems = [];
	private readonly List<int> _systemLoreSeeds = [];
	private readonly List<int> _fleetLoreSeeds = [];
	private readonly List<int[]> _planetLoreSeeds = [];

	private HashSet<(int, int)> _routeSet = [];
	private Dictionary<int, List<int>> _adjacency = [];
	private readonly List<(int From, int To, RouteNode Node)> _routeNodes = [];
	private bool _isDragging;
	private bool _hasDragCandidate;
	private int _draggingFromIndex = -1;
	private int _dragCandidateIndex = -1;
	private Vector2 _pressWorldPos;
	private Vector2 _dragWorldPos;
	private float _ghostFleetRadius;
	private Color _ghostFleetFill;
	private Color _ghostFleetOutline;
	private float _ghostFleetOutlineWidth;
	private float _defenderBonus;
	private float _systemRadius;
	private float _transitDurationSeconds;
	private PackedScene _transitFleetScene = null!;
	private PackedScene _combatEffectScene = null!;
	private Dictionary<SystemOwner, Color> _aiColors = [];
	private bool _fogEnabled;
	private float _fogClearSeconds;
	private float _fadeOutSeconds;
	private ColorRect _fadeOverlay = null!;
	private LoreConfig _loreConfig = null!;
	private SelectionPanel _selectionPanel = null!;
	private AiSystemPanel _aiSystemPanel = null!;
	private IReadOnlyList<AiPlayerData> _aiPlayers = [];
	private NotificationPanel _notificationPanel = null!;
	private EndStateConfig _endStateCfg = null!;
	private EndCondition? _activeCondition;
	private bool _endConditionReached;
	private int _objectiveSystemIndex = -1;
	private SystemOwner _targetPlayerOwner = SystemOwner.None;
	private int _defendSystemIndex = -1;
	private CountdownTimerNode? _countdownTimer;
	private string? _resolvedMissionDescription;
	private ChatWindowNode? _chatWindow;
	private BarkConfig? _barkConfig;
	private Dictionary<string, string[]> _aiDispositionBarks = [];
	private CameraController _camera = null!;
	private InfoButton _infoButton = null!;
	private AiController _aiController = null!;
	private double _lastSystemClickTime = double.MinValue;
	private int _lastClickedSystemIndex = -1;
	private RerouteButtonNode _rerouteButtonNode = null!;
	private UpgradeButtonNode _forgeButtonNode = null!;
	private UpgradeButtonNode _fortifyButtonNode = null!;
	private SplitButtonNode _splitButtonNode = null!;
	private UpgradeConfig _upgradeCfg = null!;
	private readonly Dictionary<int, int> _rerouteTargets = [];
	private readonly Dictionary<int, RerouteArrowNode> _rerouteArrows = [];
	private bool _isPickingRerouteTarget;
	private int _rerouteSourceIndex = -1;
	private PackedScene _rerouteArrowScene = null!;
	private const float RerouteMinFleet = 1.0f;
	private int _dragFleetSlot = -1;
	private int _selectedFleetSlot = -1;

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			GeneratePreview();
		else
			GenerateRuntime();
	}

	private void GeneratePreview()
	{
		Clear();
		var levelCfg = ConfigLoader.Load<LevelConfig>("res://config/level.json");
		var genCfg = ConfigLoader.Load<LevelGeneratorConfig>("res://config/level_generator.json");
		var aiCfg = ConfigLoader.Load<AiConfig>("res://config/ai.json");
		var seed = PreviewSeed != UnsetSeed ? PreviewSeed : levelCfg.DefaultPreviewSeed;
		Build(LevelGenerator.Generate(new Random(seed), genCfg, aiCfg));
	}

	private void GenerateRuntime()
	{
		var genCfg = ConfigLoader.Load<LevelGeneratorConfig>("res://config/level_generator.json");
		var aiCfg = ConfigLoader.Load<AiConfig>("res://config/ai.json");
		_barkConfig = ConfigLoader.Load<BarkConfig>("res://config/barks.json");
		_aiDispositionBarks = aiCfg.DispositionBarks;
		var data = LevelGenerator.Generate(_rng, genCfg, aiCfg);
		Build(data);
		_aiPlayers = data.AiPlayers;
		_endStateCfg = ConfigLoader.Load<EndStateConfig>("res://config/end_states.json");
		_activeCondition = _endStateCfg.Conditions[_rng.Next(_endStateCfg.Conditions.Length)];
		_resolvedMissionDescription = null;

		if (_activeCondition.TargetSystemHops.HasValue)
			_objectiveSystemIndex = FindObjectiveSystemIndex(_activeCondition.TargetSystemHops.Value);
		if (_objectiveSystemIndex >= 0)
			_systems[_objectiveSystemIndex].MarkAsObjective();

		if (_activeCondition.EliminateTargetPlayer == true && _aiPlayers.Count > 0)
			ResolveTargetPlayer();

		if (_activeCondition.DefendObjectiveSystem == true)
			ResolveDefendSystem();

		UpdateFogOfWar();
		AssignLoreSeeds(data);
		SpawnFadeOverlay();
		SpawnSelectionPanel();
		SpawnAiSystemPanel();
		SpawnNotificationPanel();
		SpawnInfoButton();
		SpawnRerouteButtonNode();
		SpawnForgeButtonNode();
		SpawnFortifyButtonNode();
		SpawnSplitButtonNode();
		SpawnAiController(data, aiCfg);
		SpawnChatWindow();

		if (_activeCondition.TimeoutSeconds.HasValue)
			SpawnCountdownTimer(_activeCondition.TimeoutSeconds.Value);

		ShowMissionBrief();
	}

	private int FindObjectiveSystemIndex(int targetHops)
	{
		var playerIndex = _systems.FindIndex(s => s.IsPlayerOwned);
		if (playerIndex < 0) return -1;

		var distances = GraphUtils.BfsHopDistances(playerIndex, _systems.Count, _routeSet);

		var maxHops = 0;
		var maxIndex = -1;
		for (var i = 0; i < distances.Length; i++)
		{
			if (distances[i] <= maxHops) continue;
			maxHops = distances[i];
			maxIndex = i;
		}

		// Upper-bound shortcut: target exceeds graph diameter, return the farthest system
		if (targetHops >= maxHops)
			return maxIndex;

		var bestIndex = -1;
		var bestDelta = int.MaxValue;
		for (var i = 0; i < distances.Length; i++)
		{
			if (i == playerIndex) continue;
			var delta = Math.Abs(distances[i] - targetHops);
			if (delta < bestDelta)
			{
				bestDelta = delta;
				bestIndex = i;
			}
		}

		return bestIndex;
	}

	private void BuildAdjacency(int systemCount)
	{
		_adjacency = new Dictionary<int, List<int>>(systemCount);
		for (var i = 0; i < systemCount; i++)
			_adjacency[i] = [];
		foreach (var (from, to) in _routeSet)
		{
			_adjacency[from].Add(to);
			_adjacency[to].Add(from);
		}
	}

	private void Build(LevelData data)
	{
		var sysCfg = ConfigLoader.Load<SystemConfig>("res://config/system.json");
		var aiCfg = ConfigLoader.Load<AiConfig>("res://config/ai.json");
		_ghostFleetRadius = sysCfg.FleetCircleRadius;
		_ghostFleetFill = sysCfg.FleetFill.ToColor();
		_ghostFleetOutline = sysCfg.FleetOutline.ToColor();
		_ghostFleetOutlineWidth = sysCfg.FleetOutlineWidth;
		_systemRadius = sysCfg.SystemRadius;
		_defenderBonus = ConfigLoader.Load<CombatConfig>("res://config/combat.json").DefenderBonus;
		var levelCfg = ConfigLoader.Load<LevelConfig>("res://config/level.json");
		_fogEnabled = levelCfg.FogEnabled;
		_fogClearSeconds = levelCfg.FogClearSeconds;
		_fadeOutSeconds = levelCfg.FadeOutSeconds;
		_transitDurationSeconds = levelCfg.TransitDurationSeconds;
		_transitFleetScene = GD.Load<PackedScene>("res://scenes/TransitFleetNode.tscn");
		_combatEffectScene = GD.Load<PackedScene>("res://scenes/CombatEffectNode.tscn");
		_rerouteArrowScene = GD.Load<PackedScene>("res://scenes/RerouteArrowNode.tscn");
		_upgradeCfg = ConfigLoader.Load<UpgradeConfig>("res://config/upgrade.json");
		_routeSet = new HashSet<(int, int)>(data.Routes);
		BuildAdjacency(data.Systems.Count);

		var camCfg = ConfigLoader.Load<CameraConfig>("res://config/camera.json");

		SpawnRoutes(data);
		SpawnSystems(data, aiCfg, sysCfg);
		SpawnCamera(data, camCfg);
	}

	private void AssignLoreSeeds(LevelData data)
	{
		_loreConfig = ConfigLoader.Load<LoreConfig>("res://config/lore.json");
		var loreRng = new Random();

		_systemLoreSeeds.Clear();
		_fleetLoreSeeds.Clear();
		_planetLoreSeeds.Clear();

		foreach (var systemData in data.Systems)
		{
			_systemLoreSeeds.Add(loreRng.Next());
			_fleetLoreSeeds.Add(loreRng.Next());

			var planetSeeds = new int[systemData.Planets.Count];
			for (var j = 0; j < planetSeeds.Length; j++)
				planetSeeds[j] = loreRng.Next();
			_planetLoreSeeds.Add(planetSeeds);
		}
	}

	private void SpawnFadeOverlay()
	{
		var layer = new CanvasLayer { Layer = 9 };
		AddChild(layer);
		_fadeOverlay = new ColorRect
		{
			Color = Colors.Black,
			Modulate = new Color(1f, 1f, 1f, 0f),
			Size = GetViewport().GetVisibleRect().Size,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		layer.AddChild(_fadeOverlay);
	}

	private void SpawnSelectionPanel()
	{
		var layer = new CanvasLayer { Layer = 10 };
		AddChild(layer);
		_selectionPanel = GD.Load<PackedScene>("res://scenes/SelectionPanel.tscn").Instantiate<SelectionPanel>();
		layer.AddChild(_selectionPanel);
	}

	private void SpawnAiSystemPanel()
	{
		var layer = new CanvasLayer { Layer = 10 };
		AddChild(layer);
		_aiSystemPanel = GD.Load<PackedScene>("res://scenes/AiSystemPanel.tscn").Instantiate<AiSystemPanel>();
		layer.AddChild(_aiSystemPanel);
	}

	private void SpawnNotificationPanel()
	{
		var layer = new CanvasLayer { Layer = 11 };
		AddChild(layer);
		_notificationPanel = GD.Load<PackedScene>("res://scenes/NotificationPanel.tscn").Instantiate<NotificationPanel>();
		layer.AddChild(_notificationPanel);
	}

	private void SpawnAiController(LevelData data, AiConfig aiCfg)
	{
		_aiController = new AiController();
		AddChild(_aiController);
		_aiController.Initialize(_systems, _routeSet, _defenderBonus, data.AiPlayers, aiCfg, _rng, OnAiActionTaken, LaunchAiTransit);
	}

	private void OnAiActionTaken()
	{
		UpdateFogOfWar();
		CheckEndCondition();
		CheckDefeatCondition();
	}

	private void SpawnInfoButton()
	{
		var layer = new CanvasLayer { Layer = 12 };
		AddChild(layer);
		var scene = GD.Load<PackedScene>("res://scenes/InfoButton.tscn");
		_infoButton = scene.Instantiate<InfoButton>();
		layer.AddChild(_infoButton);
	}

	private void SpawnRerouteButtonNode()
	{
		var layer = new CanvasLayer { Layer = 12 };
		AddChild(layer);
		var scene = GD.Load<PackedScene>("res://scenes/RerouteButtonNode.tscn");
		_rerouteButtonNode = scene.Instantiate<RerouteButtonNode>();
		layer.AddChild(_rerouteButtonNode);
	}

	private void SpawnForgeButtonNode()
	{
		var layer = new CanvasLayer { Layer = 12 };
		AddChild(layer);
		var scene = GD.Load<PackedScene>("res://scenes/ForgeButtonNode.tscn");
		_forgeButtonNode = scene.Instantiate<UpgradeButtonNode>();
		layer.AddChild(_forgeButtonNode);
	}

	private void SpawnFortifyButtonNode()
	{
		var layer = new CanvasLayer { Layer = 12 };
		AddChild(layer);
		var scene = GD.Load<PackedScene>("res://scenes/FortifyButtonNode.tscn");
		_fortifyButtonNode = scene.Instantiate<UpgradeButtonNode>();
		layer.AddChild(_fortifyButtonNode);
	}

	private void SpawnSplitButtonNode()
	{
		var layer = new CanvasLayer { Layer = 12 };
		AddChild(layer);
		var scene = GD.Load<PackedScene>("res://scenes/SplitButtonNode.tscn");
		_splitButtonNode = scene.Instantiate<SplitButtonNode>();
		layer.AddChild(_splitButtonNode);
	}

	private void ShowMissionBrief()
	{
		_notificationPanel.Show(
			"Mission",
			_resolvedMissionDescription ?? _activeCondition!.Description,
			_endStateCfg.MissionBriefSeconds,
			GetViewport().GetVisibleRect().Size,
			onDismiss: () => { }
		);
	}

	private void CheckEndCondition()
	{
		if (_endConditionReached || _activeCondition == null)
			return;
		if (!IsEndConditionMet(_activeCondition))
			return;

		_endConditionReached = true;
		_selectionPanel.Hide();
		_aiSystemPanel.Hide();
		_rerouteButtonNode.Hide();
		_forgeButtonNode.Hide();
		_fortifyButtonNode.Hide();
		_splitButtonNode.Hide();
		ShowEndSequence("Mission Complete", _activeCondition.EndDescription);
	}

	private void CheckDefeatCondition()
	{
		if (_endConditionReached)
			return;

		if (_activeCondition?.DefendObjectiveSystem == true &&
			_defendSystemIndex >= 0 &&
			!_systems[_defendSystemIndex].IsPlayerOwned)
		{
			_endConditionReached = true;
			_selectionPanel.Hide();
			_aiSystemPanel.Hide();
			_rerouteButtonNode.Hide();
			_forgeButtonNode.Hide();
			_fortifyButtonNode.Hide();
			_splitButtonNode.Hide();
			ShowEndSequence("Defeated", "The marked system has fallen. The mission is lost.");
			return;
		}

		if (_systems.Any(s => s.IsPlayerOwned))
			return;

		_endConditionReached = true;
		_selectionPanel.Hide();
		_aiSystemPanel.Hide();
		_rerouteButtonNode.Hide();
		_forgeButtonNode.Hide();
		_fortifyButtonNode.Hide();
		_splitButtonNode.Hide();
		var description = _endStateCfg.DefeatDescriptions[_rng.Next(_endStateCfg.DefeatDescriptions.Length)];
		ShowEndSequence("Defeated", description);
	}

	private void ShowEndSequence(string title, string description)
	{
		_fadeOverlay.MouseFilter = Control.MouseFilterEnum.Stop;
		_camera.PanTo(ComputeMapCenter(), _endStateCfg.EndStateSeconds);
		StartFadeOut(_fadeOutSeconds);
		_notificationPanel.Show(
			title,
			description,
			_endStateCfg.EndStateSeconds,
			GetViewport().GetVisibleRect().Size,
			onDismiss: RegenerateLevel,
			allowEarlyDismiss: false
		);
	}

	private void StartFadeOut(float durationSeconds)
	{
		var tween = CreateTween();
		tween.TweenProperty(_fadeOverlay, "modulate", new Color(1f, 1f, 1f, 1f), durationSeconds)
			.SetTrans(Tween.TransitionType.Linear);
	}

	private Vector2 ComputeMapCenter()
	{
		var sum = Vector2.Zero;
		foreach (var system in _systems)
			sum += system.GlobalPosition;
		return sum / _systems.Count;
	}

	private bool IsEndConditionMet(EndCondition condition)
	{
		if (condition.EnemiesLeft.HasValue)
		{
			var enemiesWithFleet = _systems.Count(s => !s.IsPlayerOwned && s.HasFleet);
			if (enemiesWithFleet > condition.EnemiesLeft.Value)
				return false;
		}

		if (condition.SystemsLeft.HasValue)
		{
			var nonPlayerSystems = _systems.Count(s => !s.IsPlayerOwned);
			if (nonPlayerSystems > condition.SystemsLeft.Value)
				return false;
		}

		if (condition.TargetSystemHops.HasValue)
		{
			if (_objectiveSystemIndex < 0 || !_systems[_objectiveSystemIndex].IsPlayerOwned)
				return false;
		}

		if (condition.EliminateTargetPlayer == true)
		{
			if (_targetPlayerOwner == SystemOwner.None)
				return false;
			if (_systems.Any(s => s.Owner == _targetPlayerOwner && s.HasFleet))
				return false;
		}

		if (condition.DefendObjectiveSystem == true)
		{
			if (_defendSystemIndex < 0 || !_systems[_defendSystemIndex].IsPlayerOwned)
				return false;
		}

		return true;
	}

	private void RegenerateLevel()
	{
		Clear();
		GenerateRuntime();
	}

	private void Clear()
	{
		foreach (var child in GetChildren())
			child.QueueFree();
		_systems.Clear();
		_routeSet.Clear();
		_adjacency.Clear();
		_routeNodes.Clear();
		_isDragging = false;
		_hasDragCandidate = false;
		_draggingFromIndex = -1;
		_dragCandidateIndex = -1;
		_fadeOverlay = null!;
		_selectionPanel = null!;
		_aiSystemPanel = null!;
		_aiPlayers = [];
		_notificationPanel = null!;
		_activeCondition = null;
		_endConditionReached = false;
		_objectiveSystemIndex = -1;
		_targetPlayerOwner = SystemOwner.None;
		_defendSystemIndex = -1;
		_countdownTimer = null;
		_resolvedMissionDescription = null;
		_chatWindow = null;
		_camera = null!;
		_infoButton = null!;
		_rerouteButtonNode = null!;
		_forgeButtonNode = null!;
		_fortifyButtonNode = null!;
		_splitButtonNode = null!;
		_aiController = null!;
		_lastSystemClickTime = double.MinValue;
		_lastClickedSystemIndex = -1;
		_dragFleetSlot = -1;
		_selectedFleetSlot = -1;
		_rerouteTargets.Clear();
		_rerouteArrows.Clear();
		_isPickingRerouteTarget = false;
		_rerouteSourceIndex = -1;
		_rerouteArrowScene = null!;
		_upgradeCfg = null!;
	}

	private void SpawnRoutes(LevelData data)
	{
		foreach (var (from, to) in data.Routes)
		{
			var fromPos = data.Systems[from].Position;
			var toPos = data.Systems[to].Position;
			var route = new RouteNode();
			AddChild(route);
			route.Initialize(
				EdgeToward(fromPos, toPos, _systemRadius),
				EdgeToward(toPos, fromPos, _systemRadius));
			_routeNodes.Add((from, to, route));
		}
	}

	private static Vector2 EdgeToward(Vector2 origin, Vector2 target, float radius)
		=> origin + (target - origin).Normalized() * radius;

	private void UpdateFogOfWar()
	{
		if (!_fogEnabled)
		{
			for (var i = 0; i < _systems.Count; i++)
				_systems[i].SetFogState(FogState.Revealed, _fogClearSeconds);
			foreach (var (from, to, routeNode) in _routeNodes)
			{
				routeNode.SetFogState(FogState.Revealed, _fogClearSeconds);
				routeNode.SetOwnerColor(SharedOwnerColor(from, to));
			}
			return;
		}

		var scoutedByPlayer = new HashSet<int>();
		for (var i = 0; i < _systems.Count; i++)
		{
			if (!_systems[i].IsPlayerOwned) continue;
			foreach (var neighbor in _adjacency[i])
				scoutedByPlayer.Add(neighbor);
		}

		for (var i = 0; i < _systems.Count; i++)
		{
			FogState state;
			if (_systems[i].IsPlayerOwned)
				state = FogState.Revealed;
			else if (scoutedByPlayer.Contains(i))
				state = FogState.Scouted;
			else
				state = FogState.Hidden;
			_systems[i].SetFogState(state, _fogClearSeconds);
		}

		if (_objectiveSystemIndex >= 0 && _systems[_objectiveSystemIndex].FogState == FogState.Hidden)
			_systems[_objectiveSystemIndex].SetFogState(FogState.Scouted, _fogClearSeconds);

		foreach (var (from, to, routeNode) in _routeNodes)
		{
			var fromState = _systems[from].FogState;
			var toState = _systems[to].FogState;

			FogState routeState;
			if (fromState == FogState.Hidden && toState == FogState.Hidden)
				routeState = FogState.Hidden;
			else if (fromState == FogState.Revealed || toState == FogState.Revealed)
				routeState = FogState.Revealed;
			else
				routeState = FogState.Scouted;

			routeNode.SetFogState(routeState, _fogClearSeconds);
			routeNode.SetOwnerColor(SharedOwnerColor(from, to));
		}
	}

	private void SpawnSystems(LevelData data, AiConfig aiCfg, SystemConfig sysCfg)
	{
		_aiColors = BuildAiColors(data.AiPlayers, aiCfg);
		_aiColors[SystemOwner.Player] = sysCfg.FleetOutline.ToColor();

		foreach (var systemData in data.Systems)
		{
			var system = new SystemNode();
			system.Position = systemData.Position;
			AddChild(system);
			var aiPlayer = data.AiPlayers.FirstOrDefault(a => a.Owner == systemData.Owner);
			var aiColor = aiPlayer != null ? _aiColors[aiPlayer.Owner] : (Color?)null;
			system.Initialize(systemData.Planets, systemData.Owner, systemData.InitialFleet, aiPlayer, aiColor);
			_systems.Add(system);
		}
	}

	private void ResolveTargetPlayer()
	{
		var target = _aiPlayers[_rng.Next(_aiPlayers.Count)];
		_targetPlayerOwner = target.Owner;
		foreach (var system in _systems)
			system.SetTargetOwner(_targetPlayerOwner);
		_resolvedMissionDescription = $"{target.FactionName} marked for elimination. Destroy them before time runs out.";
	}

	private void ResolveDefendSystem()
	{
		_defendSystemIndex = _systems.FindIndex(s => s.IsPlayerOwned);
		if (_defendSystemIndex >= 0)
			_systems[_defendSystemIndex].MarkAsDefend();
	}

	private void SpawnCountdownTimer(float seconds)
	{
		var layer = new CanvasLayer { Layer = 10 };
		AddChild(layer);
		var scene = GD.Load<PackedScene>("res://scenes/CountdownTimerNode.tscn");
		_countdownTimer = scene.Instantiate<CountdownTimerNode>();
		layer.AddChild(_countdownTimer);
		var viewportSize = GetViewport().GetVisibleRect().Size;
		const float timerWidth = 90f;
		const float timerPad = 8f;
		_countdownTimer.Position = new Vector2(viewportSize.X - timerWidth - timerPad, timerPad);
		_countdownTimer.Initialize(seconds, OnCountdownExpired);
	}

	private void SpawnChatWindow()
	{
		var layer = new CanvasLayer { Layer = 10 };
		AddChild(layer);
		var scene = GD.Load<PackedScene>("res://scenes/ChatWindowNode.tscn");
		_chatWindow = scene.Instantiate<ChatWindowNode>();
		layer.AddChild(_chatWindow);
		var viewportSize = GetViewport().GetVisibleRect().Size;
		const float chatHeight = 54f;
		const float sidePad = 8f;
		const float bottomPad = 8f;
		_chatWindow.Position = new Vector2(sidePad, viewportSize.Y - chatHeight - bottomPad);
	}

	private void PostPlayerTransitBark(int toIndex)
	{
		var pool = _systems[toIndex].Owner == SystemOwner.Player
			? _barkConfig?.PlayerMove
			: _barkConfig?.PlayerAttack;
		PostBark(pool);
	}

	private void PostBark(Bark[]? pool)
	{
		if (pool == null || pool.Length == 0 || _chatWindow == null)
			return;
		var bark = pool[_rng.Next(pool.Length)];
		_chatWindow.PostMessage(bark.Npc, bark.Message);
	}

	private void PostAiBark(AiPlayerData aiPlayer)
	{
		if (_chatWindow == null)
			return;
		if (!_aiDispositionBarks.TryGetValue(aiPlayer.Disposition.ToString(), out var messages) || messages.Length == 0)
			return;
		var message = messages[_rng.Next(messages.Length)];
		_chatWindow.PostMessage(aiPlayer.FactionName, message);
	}

	private void OnCountdownExpired()
	{
		if (_endConditionReached)
			return;
		_endConditionReached = true;
		_selectionPanel.Hide();
		_aiSystemPanel.Hide();
		_rerouteButtonNode.Hide();
		_forgeButtonNode.Hide();
		_fortifyButtonNode.Hide();
		_splitButtonNode.Hide();
		ShowEndSequence("Time Expired", _activeCondition?.TimeoutMessage ?? "The mission clock has run out.");
	}

	private Color? SharedOwnerColor(int fromIndex, int toIndex)
	{
		var fromOwner = _systems[fromIndex].Owner;
		var toOwner = _systems[toIndex].Owner;
		if (fromOwner == SystemOwner.None || fromOwner != toOwner)
			return null;
		return _aiColors.TryGetValue(fromOwner, out var color) ? color : null;
	}

	private Dictionary<SystemOwner, Color> BuildAiColors(IReadOnlyList<AiPlayerData> aiPlayers, AiConfig aiCfg)
	{
		var colors = new Dictionary<SystemOwner, Color>();
		foreach (var player in aiPlayers)
		{
			var baseColor = aiCfg.DispositionColors[player.Disposition.ToString()].ToColor();
			var hueShift = (float)(_rng.NextDouble() * 0.2 - 0.1);
			baseColor.ToHsv(out var h, out var s, out var v);
			h = (h + hueShift + 1f) % 1f;
			colors[player.Owner] = Color.FromHsv(h, s, v, baseColor.A);
		}
		return colors;
	}

	private void SpawnCamera(LevelData data, CameraConfig camCfg)
	{
		if (Engine.IsEditorHint())
			return;

		var playerSystem = data.Systems.FirstOrDefault(s => s.Owner == SystemOwner.Player);
		_camera = new CameraController();
		AddChild(_camera);
		_camera.MakeCurrent();
		_camera.FocusOn(playerSystem?.Position ?? Vector2.Zero, camCfg.StartZoom);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Engine.IsEditorHint() || _endConditionReached)
			return;

		if (@event is InputEventMouseButton mb)
			HandleMouseButton(mb);
		else if (@event is InputEventMouseMotion)
			HandleMouseMotion();
	}

	private void HandleMouseButton(InputEventMouseButton e)
	{
		if (e.ButtonIndex != MouseButton.Left)
			return;

		if (e.Pressed)
		{
			_pressWorldPos = GetGlobalMousePosition();
			TryRegisterDragCandidate();
		}
		else if (_isDragging)
		{
			EndFleetDrag();
			_hasDragCandidate = false;
			_dragCandidateIndex = -1;
		}
		else
		{
			HandleClick(_pressWorldPos);
			_hasDragCandidate = false;
			_dragCandidateIndex = -1;
		}
	}

	private void HandleMouseMotion()
	{
		if (_hasDragCandidate && !_isDragging)
		{
			var worldPos = GetGlobalMousePosition();
			if (worldPos.DistanceTo(_pressWorldPos) > DragThreshold)
			{
				_isDragging = true;
				_draggingFromIndex = _dragCandidateIndex;
				_dragWorldPos = worldPos;
				_systems[_draggingFromIndex].SetSelected(true);
				QueueRedraw();
			}
			GetViewport().SetInputAsHandled();
		}
		else if (_isDragging)
		{
			_dragWorldPos = GetGlobalMousePosition();
			QueueRedraw();
			GetViewport().SetInputAsHandled();
		}
	}

	private void TryRegisterDragCandidate()
	{
		for (var i = 0; i < _systems.Count; i++)
		{
			if (!_systems[i].IsPlayerOwned || !_systems[i].HasFleet)
				continue;
			var slot = _systems[i].GetFleetSlotAt(_pressWorldPos);
			if (slot < 0) continue;

			_dragCandidateIndex = i;
			_dragFleetSlot = slot;
			_hasDragCandidate = true;
			GetViewport().SetInputAsHandled();
			return;
		}
	}

	private void HandleClick(Vector2 worldPos)
	{
		if (_isPickingRerouteTarget)
		{
			HandleRerouteTargetPick(worldPos);
			return;
		}

		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].FogState == FogState.Hidden) continue;

			var fleetSlot = _systems[i].GetFleetSlotAt(worldPos);
			if (_systems[i].HasFleet && fleetSlot >= 0)
			{
				_selectedFleetSlot = fleetSlot;
				SelectFleet(i);
				return;
			}

			var planetIndex = _systems[i].PlanetIndexAt(worldPos);
			if (planetIndex.HasValue)
			{
				ShowPlanetInfo(i, planetIndex.Value);
				return;
			}

			if (_systems[i].ContainsSystemAt(worldPos))
			{
				HandleSystemClick(i);
				return;
			}
		}

		_aiSystemPanel.Hide();
		_camera.ExitFollowMode();
		_infoButton.Hide();
		_rerouteButtonNode.Hide();
		_forgeButtonNode.Hide();
		_fortifyButtonNode.Hide();
		_splitButtonNode.Hide();
		_selectionPanel.Hide();
	}

	private void SelectFleet(int systemIndex)
	{
		var viewportSize = GetViewport().GetVisibleRect().Size;
		_infoButton.ShowFor(viewportSize, () => ShowFleetInfo(systemIndex));
		if (_systems[systemIndex].IsPlayerOwned)
		{
			_rerouteButtonNode.ShowFor(viewportSize, _rerouteTargets.ContainsKey(systemIndex), () => OnRerouteButtonPressed(systemIndex));
			ShowPlayerUpgradeButtons(systemIndex);
			ShowSplitButton(systemIndex, _selectedFleetSlot);
		}
		else
		{
			_rerouteButtonNode.Hide();
			_forgeButtonNode.Hide();
			_fortifyButtonNode.Hide();
			_splitButtonNode.Hide();
		}
	}

	private void SelectSystem(int systemIndex)
	{
		var viewportSize = GetViewport().GetVisibleRect().Size;
		_infoButton.ShowFor(viewportSize, () => ShowSystemInfo(systemIndex));
		if (_systems[systemIndex].IsPlayerOwned)
		{
			_rerouteButtonNode.ShowFor(viewportSize, _rerouteTargets.ContainsKey(systemIndex), () => OnRerouteButtonPressed(systemIndex));
			ShowPlayerUpgradeButtons(systemIndex);
		}
		else
		{
			_rerouteButtonNode.Hide();
			_forgeButtonNode.Hide();
			_fortifyButtonNode.Hide();
		}
		_splitButtonNode.Hide();
	}

	private void SelectAiSystem(int systemIndex)
	{
		_infoButton.ShowFor(GetViewport().GetVisibleRect().Size, () => ShowAiSystemInfo(systemIndex));
		_rerouteButtonNode.Hide();
		_forgeButtonNode.Hide();
		_fortifyButtonNode.Hide();
		_splitButtonNode.Hide();
	}

	private void ShowAiSystemInfo(int systemIndex)
	{
		var owner = _systems[systemIndex].Owner;
		var aiPlayer = _aiPlayers.FirstOrDefault(p => p.Owner == owner);
		if (aiPlayer == null) return;
		_aiColors.TryGetValue(owner, out var color);
		_selectionPanel.Hide();
		_aiSystemPanel.ShowFor(aiPlayer, color, _systemLoreSeeds[systemIndex], GetViewport().GetVisibleRect().Size);
	}

	private void HandleSystemClick(int systemIndex)
	{
		var now = Time.GetTicksMsec() / 1000.0;
		var isDoubleClick = _lastClickedSystemIndex == systemIndex
			&& (now - _lastSystemClickTime) < DoubleClickThresholdSeconds;
		_lastSystemClickTime = now;
		_lastClickedSystemIndex = systemIndex;

		if (isDoubleClick)
			_camera.FollowSystem(_systems[systemIndex].GlobalPosition);
		else if (_systems[systemIndex].IsAiOwned)
			SelectAiSystem(systemIndex);
		else
			SelectSystem(systemIndex);
	}

	private void ShowFleetInfo(int systemIndex)
	{
		var pool = _systems[systemIndex].IsPlayerOwned ? _loreConfig.PlayerFleet : _loreConfig.NeutralFleet;
		var seed = _fleetLoreSeeds[systemIndex];
		var title = Pick(pool.Titles, seed);
		var description = Pick(pool.Descriptions, seed);
		_aiSystemPanel.Hide();
		_selectionPanel.ShowAt($"Fleet — {title}", description, GetViewport().GetVisibleRect().Size);
	}

	private void ShowSystemInfo(int systemIndex)
	{
		var seed = _systemLoreSeeds[systemIndex];
		var title = Pick(_loreConfig.System.Titles, seed);
		var description = Pick(_loreConfig.System.Descriptions, seed);
		_aiSystemPanel.Hide();
		_selectionPanel.ShowAt(title, description, GetViewport().GetVisibleRect().Size);
	}

	private void ShowPlayerUpgradeButtons(int systemIndex)
	{
		var viewportSize = GetViewport().GetVisibleRect().Size;
		var upgrade = _systems[systemIndex].Upgrade;
		var canAfford = _systems[systemIndex].Ships >= _upgradeCfg.UpgradeCost;

		var fortifyLabel = upgrade == SystemUpgrade.Fortify ? "Remove\nFortify" : "Fortify\nSystem";
		var fortifyDisabled = upgrade == SystemUpgrade.Fortify ? false : (upgrade != SystemUpgrade.None || !canAfford);
		Action fortifyAction = upgrade == SystemUpgrade.Fortify
			? () => DoUpgrade(systemIndex, SystemUpgrade.None)
			: () => DoUpgrade(systemIndex, SystemUpgrade.Fortify);

		var forgeLabel = upgrade == SystemUpgrade.Forge ? "Remove\nForge" : "Forge\nSystem";
		var forgeDisabled = upgrade == SystemUpgrade.Forge ? false : (upgrade != SystemUpgrade.None || !canAfford);
		Action forgeAction = upgrade == SystemUpgrade.Forge
			? () => DoUpgrade(systemIndex, SystemUpgrade.None)
			: () => DoUpgrade(systemIndex, SystemUpgrade.Forge);

		_fortifyButtonNode.ShowFor(viewportSize, slotFromRight: 3, label: fortifyLabel, disabled: fortifyDisabled, onPressed: fortifyAction);
		_forgeButtonNode.ShowFor(viewportSize, slotFromRight: 4, label: forgeLabel, disabled: forgeDisabled, onPressed: forgeAction);
	}

	private void ShowSplitButton(int systemIndex, int fleetSlot)
	{
		var viewportSize = GetViewport().GetVisibleRect().Size;
		var ships = _systems[systemIndex].GetFleetShips(fleetSlot);
		_splitButtonNode.ShowFor(viewportSize, disabled: ships < 2f, onPressed: () => OnSplitButtonPressed(systemIndex, fleetSlot));
	}

	private void OnSplitButtonPressed(int systemIndex, int fleetSlot)
	{
		_systems[systemIndex].SplitFleet(fleetSlot);
		ShowSplitButton(systemIndex, fleetSlot);
	}

	private void DoUpgrade(int systemIndex, SystemUpgrade upgrade)
	{
		if (upgrade != SystemUpgrade.None)
			_systems[systemIndex].SpendShips(_upgradeCfg.UpgradeCost);
		_systems[systemIndex].ApplyUpgrade(upgrade);

		var pool = upgrade switch
		{
			SystemUpgrade.Forge => _barkConfig?.PlayerForge,
			SystemUpgrade.Fortify => _barkConfig?.PlayerFortify,
			_ => null
		};
		PostBark(pool);

		ShowPlayerUpgradeButtons(systemIndex);
	}

	private void ShowPlanetInfo(int systemIndex, int planetIndex)
	{
		var seed = _planetLoreSeeds[systemIndex][planetIndex];
		var title = Pick(_loreConfig.Planet.Titles, seed);
		var description = Pick(_loreConfig.Planet.Descriptions, seed);
		_selectionPanel.ShowAt(title, description, GetViewport().GetVisibleRect().Size);
	}

	private static string Pick(string[] pool, int seed) => pool[seed % pool.Length];

	private void EndFleetDrag()
	{
		var worldPos = GetGlobalMousePosition();
		var resolved = false;

		for (var i = 0; i < _systems.Count; i++)
		{
			if (i == _draggingFromIndex || !_systems[i].ContainsSystemAt(worldPos))
				continue;
			if (!AreConnected(_draggingFromIndex, i))
				continue;

			var fromIndex = _draggingFromIndex;
			var toIndex = i;
			var fleet = _systems[fromIndex].TakeFleet(_dragFleetSlot);

			PostPlayerTransitBark(toIndex);

			var fromEdge = EdgeToward(_systems[fromIndex].Position, _systems[toIndex].Position, _systemRadius);
			var toEdge = EdgeToward(_systems[toIndex].Position, _systems[fromIndex].Position, _systemRadius);

			var transit = _transitFleetScene.Instantiate<TransitFleetNode>();
			AddChild(transit);
			transit.Launch(fromEdge, toEdge, _ghostFleetOutline, _transitDurationSeconds,
				() => ResolvePlayerTransitArrival(toIndex, fleet));

			resolved = true;
			break;
		}

		if (!resolved)
			_systems[_draggingFromIndex].SetSelected(false);

		_isDragging = false;
		_draggingFromIndex = -1;
		_dragFleetSlot = -1;
		QueueRedraw();

		if (resolved)
			UpdateFogOfWar();
	}

	private void ResolvePlayerTransitArrival(int toIndex, float fleet)
	{
		var target = _systems[toIndex];

		if (target.Owner == SystemOwner.Player)
		{
			target.AddFleet(fleet);
		}
		else
		{
			var defBonus = _defenderBonus * (1f + target.DefenseBonusMultiplier);
			var result = CombatResolver.Resolve(fleet, target.Ships, defBonus);
			if (result.AttackerWins)
			{
				target.Capture(result.AttackerRemainder, SystemOwner.Player);
				SpawnCombatEffect(toIndex, attackerWon: true);
				if (_camera.IsFollowing)
					_camera.FollowSystem(target.GlobalPosition);
			}
			else
			{
				target.SustainDefense(result.DefenderRemainder);
				SpawnCombatEffect(toIndex, attackerWon: false);
			}
		}

		UpdateFogOfWar();
		CheckEndCondition();
		CheckDefeatCondition();
	}

	private void LaunchAiTransit(int fromIndex, int toIndex, float fleet, AiPlayerData aiPlayer)
	{
		if (_systems[toIndex].Owner == SystemOwner.Player)
		{
			PostBark(_barkConfig?.PlayerUnderAttack);
			PostAiBark(aiPlayer);
		}

		var dotColor = _aiColors[aiPlayer.Owner];
		var fromEdge = EdgeToward(_systems[fromIndex].Position, _systems[toIndex].Position, _systemRadius);
		var toEdge = EdgeToward(_systems[toIndex].Position, _systems[fromIndex].Position, _systemRadius);

		var transit = _transitFleetScene.Instantiate<TransitFleetNode>();
		AddChild(transit);
		transit.Launch(fromEdge, toEdge, dotColor, _transitDurationSeconds,
			() => ResolveAiTransitArrival(toIndex, fleet, aiPlayer.Owner, aiPlayer, dotColor));
	}

	private void ResolveAiTransitArrival(int toIndex, float fleet, SystemOwner senderOwner, AiPlayerData aiPlayer, Color aiOwnerColor)
	{
		var target = _systems[toIndex];

		if (target.Owner == senderOwner)
		{
			target.AddFleet(fleet);
		}
		else
		{
			var defBonus = _defenderBonus * (1f + target.DefenseBonusMultiplier);
			var result = CombatResolver.Resolve(fleet, target.Ships, defBonus);
			if (result.AttackerWins)
			{
				target.Capture(result.AttackerRemainder, senderOwner, aiPlayer, aiOwnerColor);
				SpawnCombatEffect(toIndex, attackerWon: true);
				ClearReroute(toIndex);
			}
			else
			{
				target.SustainDefense(result.DefenderRemainder);
				SpawnCombatEffect(toIndex, attackerWon: false);
			}
		}

		OnAiActionTaken();
	}

	private void SpawnCombatEffect(int systemIndex, bool attackerWon)
	{
		var pos = _systems[systemIndex].Position;

		var impact = _combatEffectScene.Instantiate<CombatEffectNode>();
		AddChild(impact);
		impact.Position = pos;
		impact.PlayImpact();

		if (!attackerWon)
			return;

		var capture = _combatEffectScene.Instantiate<CombatEffectNode>();
		AddChild(capture);
		capture.Position = pos;
		capture.PlayCapture();
	}

	public override void _Draw()
	{
		if (!_isDragging)
			return;

		DrawCircle(_dragWorldPos, _ghostFleetRadius, _ghostFleetFill);
		DrawArc(_dragWorldPos, _ghostFleetRadius, 0f, Mathf.Tau, 32, _ghostFleetOutline, _ghostFleetOutlineWidth);
	}

	private bool AreConnected(int a, int b)
	{
		var edge = a < b ? (a, b) : (b, a);
		return _routeSet.Contains(edge);
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || _endConditionReached)
			return;
		ProcessReroutes();
	}

	private void ProcessReroutes()
	{
		var fogUpdateNeeded = false;
		foreach (var (sourceIndex, targetIndex) in _rerouteTargets)
		{
			var source = _systems[sourceIndex];
			if (!source.IsPlayerOwned || source.Ships < RerouteMinFleet)
				continue;
			if (!AreConnected(sourceIndex, targetIndex))
				continue;

			var fleet = source.TakeFleet();
			var fromEdge = EdgeToward(source.Position, _systems[targetIndex].Position, _systemRadius);
			var toEdge = EdgeToward(_systems[targetIndex].Position, source.Position, _systemRadius);
			var transit = _transitFleetScene.Instantiate<TransitFleetNode>();
			AddChild(transit);
			var si = sourceIndex;
			var ti = targetIndex;
			var f = fleet;
			transit.Launch(fromEdge, toEdge, _ghostFleetOutline, _transitDurationSeconds,
				() => ResolvePlayerTransitArrival(ti, f));
			fogUpdateNeeded = true;
		}

		if (fogUpdateNeeded)
			UpdateFogOfWar();
	}

	private void OnRerouteButtonPressed(int systemIndex)
	{
		if (_rerouteTargets.ContainsKey(systemIndex))
		{
			ClearReroute(systemIndex);
			_rerouteButtonNode.UpdateRouteState(hasActiveRoute: false);
		}
		else
		{
			_isPickingRerouteTarget = true;
			_rerouteSourceIndex = systemIndex;
			_rerouteButtonNode.Hide();
			_forgeButtonNode.Hide();
			_fortifyButtonNode.Hide();
			_splitButtonNode.Hide();
			_infoButton.Hide();
			_selectionPanel.Hide();
		}
	}

	private void HandleRerouteTargetPick(Vector2 worldPos)
	{
		_isPickingRerouteTarget = false;

		for (var i = 0; i < _systems.Count; i++)
		{
			if (i == _rerouteSourceIndex) continue;
			if (_systems[i].FogState == FogState.Hidden) continue;
			if (!AreConnected(_rerouteSourceIndex, i)) continue;
			if (!_systems[i].ContainsSystemAt(worldPos)) continue;

			var si = _rerouteSourceIndex;
			SetRerouteTarget(si, i);
			var viewportSize = GetViewport().GetVisibleRect().Size;
			_rerouteButtonNode.ShowFor(viewportSize, hasActiveRoute: true, () => OnRerouteButtonPressed(si));
			ShowPlayerUpgradeButtons(si);
			_rerouteSourceIndex = -1;
			return;
		}

		_rerouteSourceIndex = -1;
	}

	private void SetRerouteTarget(int sourceIndex, int targetIndex)
	{
		ClearReroute(sourceIndex);
		_rerouteTargets[sourceIndex] = targetIndex;

		var arrow = _rerouteArrowScene.Instantiate<RerouteArrowNode>();
		_systems[sourceIndex].AddChild(arrow);
		arrow.Initialize(_systems[sourceIndex].GlobalPosition, _systems[targetIndex].GlobalPosition, _systemRadius);
		_rerouteArrows[sourceIndex] = arrow;
	}

	private void ClearReroute(int systemIndex)
	{
		if (_rerouteArrows.TryGetValue(systemIndex, out var arrow))
		{
			arrow.QueueFree();
			_rerouteArrows.Remove(systemIndex);
		}
		_rerouteTargets.Remove(systemIndex);
	}
}
