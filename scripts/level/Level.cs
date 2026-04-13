using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tts;

[Tool]
public partial class Level : Node2D
{
	// Tracks a fleet in transit for route-combat detection.
	private sealed class ActiveTransit
	{
		public required int FromIndex;
		public required int ToIndex;
		public required SystemOwner Owner;
		public required float Fleet;
		public required double LaunchTimeSec;
		public required float TotalDurationSec;
		public required TransitFleetNode Node;
		public required Vector2 ToWorldPos;
		// Given a surviving fleet count, returns the action to invoke on arrival.
		public required Func<float, Action> ArrivalCallback;
	}

	// Groups the six variables that describe one in-flight drag gesture.
	private struct DragState
	{
		public bool IsActive;
		public bool HasCandidate;
		public int FromIndex;
		public int CandidateIndex;
		public int FleetSlot;
		public Vector2 WorldPos;

		public static readonly DragState None = new() { FromIndex = -1, CandidateIndex = -1, FleetSlot = -1 };
	}

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

	private HashSet<(int, int)> _routeSet = [];
	private Dictionary<int, List<int>> _adjacency = [];
	private readonly List<(int From, int To, RouteNode Node)> _routeNodes = [];

	private DragState _drag = DragState.None;
	private Vector2 _pressWorldPos;

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
	private CameraController _camera = null!;
	private SystemActionMenu _systemActionMenu = null!;
	private AiController _aiController = null!;
	private NarrativeController? _narrativeController;
	private InterludeContent? _pendingInterlude;
	private GameMode? _gameModeOverride;
	private double _lastSystemClickTime = double.MinValue;
	private int _lastClickedSystemIndex = -1;
	private UpgradeConfig _upgradeCfg = null!;
	private readonly Dictionary<int, int> _rerouteTargets = [];
	private readonly Dictionary<int, RerouteArrowNode> _rerouteArrows = [];
	private readonly List<ActiveTransit> _activeTransits = [];
	private bool _isPickingRerouteTarget;
	private int? _rerouteSourceIndex;
	private PackedScene _rerouteArrowScene = null!;
	private const float RerouteMinFleet = 1.0f;
	private int _selectedFleetSlot = -1;
	private DebugOverlay _debugOverlay = null!;

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			GeneratePreview();
		else
			GenerateRuntime();
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || _endConditionReached)
			return;
		ProcessReroutes();
	}

	public override void _Draw()
	{
		if (!_drag.IsActive) return;
		DrawCircle(_drag.WorldPos, _ghostFleetRadius, _ghostFleetFill);
		DrawArc(_drag.WorldPos, _ghostFleetRadius, 0f, Mathf.Tau, 32, _ghostFleetOutline, _ghostFleetOutlineWidth);
	}

	private void GeneratePreview()
	{
		Clear();
		var levelCfg = ConfigLoader.Load<LevelConfig>("res://config/level.json");
		var genCfg = ConfigLoader.Load<LevelGeneratorConfig>("res://config/level_generator.json");
		var aiCfg = ConfigLoader.Load<AiConfig>("res://config/ai.json");
		var aiNamingCfg = ConfigLoader.Load<AiNamingConfig>("res://config/ai_naming.json");
		var seed = PreviewSeed != UnsetSeed ? PreviewSeed : levelCfg.DefaultPreviewSeed;
		Build(LevelGenerator.Generate(new Random(seed), genCfg, aiCfg, aiNamingCfg));
	}

	private void GenerateRuntime()
	{
		var genCfg = ConfigLoader.Load<LevelGeneratorConfig>("res://config/level_generator.json");
		var aiCfg = ConfigLoader.Load<AiConfig>("res://config/ai.json");
		var aiNamingCfg = ConfigLoader.Load<AiNamingConfig>("res://config/ai_naming.json");

		_barkConfig = ConfigLoader.Load<BarkConfig>("res://config/barks.json");
		var data = LevelGenerator.Generate(_rng, genCfg, aiCfg, aiNamingCfg);
		Build(data);
		_aiPlayers = data.AiPlayers;
		_endStateCfg = ConfigLoader.Load<EndStateConfig>("res://config/end_states.json");

		if (_narrativeController == null)
		{
			var gameMode = _gameModeOverride ?? ConfigLoader.Load<GameModeConfig>("res://config/game_mode.json").Mode;
			if (gameMode == GameMode.Story)
			{
				_narrativeController = NarrativeController.Create(_rng);
				_narrativeController.StartCampaign();
			}
		}

		if (_narrativeController != null && !_narrativeController.IsCampaignComplete)
		{
			var primaryAi = _aiPlayers.Count > 0 ? _aiPlayers[_rng.Next(_aiPlayers.Count)] : null;
			if (primaryAi != null)
				_narrativeController.UpdateEnemy(primaryAi);
			var missionContext = _narrativeController.GetNextMission();
			_activeCondition = missionContext.Condition.ToEndCondition();
			_resolvedMissionDescription = missionContext.Briefing;
			_pendingInterlude = missionContext.Interlude;
		}
		else
		{
			_activeCondition = _endStateCfg.Conditions[_rng.Next(_endStateCfg.Conditions.Length)];
			_resolvedMissionDescription = null;
		}

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
		SpawnSystemActionMenu();
		SpawnAiController(data, aiCfg);
		SpawnChatWindow();

		if (_activeCondition.TimeoutSeconds.HasValue)
			SpawnCountdownTimer(_activeCondition.TimeoutSeconds.Value);

		SpawnDebugOverlay();

		if (_pendingInterlude != null)
			SpawnInterludePanel(_pendingInterlude.Text, onDismiss: ShowMissionBrief);
		else
			ShowMissionBrief();
	}

	private void SpawnDebugOverlay()
	{
		_debugOverlay = new DebugOverlay();
		AddChild(_debugOverlay);
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
		_ghostFleetRadius = sysCfg.LabelHeight / 2f;
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
		_transitFleetScene = GD.Load<PackedScene>("res://scenes/fleet/TransitFleetNode.tscn");
		_combatEffectScene = GD.Load<PackedScene>("res://scenes/effects/CombatEffectNode.tscn");
		_rerouteArrowScene = GD.Load<PackedScene>("res://scenes/system/RerouteArrowNode.tscn");
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

		foreach (var systemData in data.Systems)
		{
			_systemLoreSeeds.Add(loreRng.Next());
			_fleetLoreSeeds.Add(loreRng.Next());
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
		_selectionPanel = GD.Load<PackedScene>("res://scenes/ui/SelectionPanel.tscn").Instantiate<SelectionPanel>();
		layer.AddChild(_selectionPanel);
	}

	private void SpawnAiSystemPanel()
	{
		var layer = new CanvasLayer { Layer = 10 };
		AddChild(layer);
		_aiSystemPanel = GD.Load<PackedScene>("res://scenes/ui/AiSystemPanel.tscn").Instantiate<AiSystemPanel>();
		layer.AddChild(_aiSystemPanel);
	}

	private void SpawnNotificationPanel()
	{
		var layer = new CanvasLayer { Layer = 11 };
		AddChild(layer);
		_notificationPanel = GD.Load<PackedScene>("res://scenes/ui/NotificationPanel.tscn").Instantiate<NotificationPanel>();
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

	private void SpawnSystemActionMenu()
	{
		var layer = new CanvasLayer { Layer = 12 };
		AddChild(layer);
		_systemActionMenu = GD.Load<PackedScene>("res://scenes/ui/SystemActionMenu.tscn").Instantiate<SystemActionMenu>();
		layer.AddChild(_systemActionMenu);
		var cfg = ConfigLoader.Load<ActionMenuConfig>("res://config/action_menu.json");
		_systemActionMenu.Initialize(_camera, _systemRadius, cfg);
	}

	private void SpawnCountdownTimer(float seconds)
	{
		var layer = new CanvasLayer { Layer = 10 };
		AddChild(layer);
		_countdownTimer = GD.Load<PackedScene>("res://scenes/ui/CountdownTimerNode.tscn").Instantiate<CountdownTimerNode>();
		layer.AddChild(_countdownTimer);
		var viewportSize = GetViewport().GetVisibleRect().Size;
		const float timerWidth = 90f;
		const float timerPad = 8f;
		_countdownTimer.Position = new Vector2(viewportSize.X - timerWidth - timerPad, timerPad);
		_countdownTimer.Initialize(seconds, OnCountdownExpired);
	}

	private void OnEndSequenceDismissed()
	{
		if (_narrativeController?.IsCampaignComplete == true)
			SpawnOutroPanel();
		else
			RegenerateLevel();
	}

	private void SpawnOutroPanel()
	{
		var (title, text) = _narrativeController!.GenerateOutro();
		var layer = new CanvasLayer { Layer = 14 };
		AddChild(layer);
		var panel = GD.Load<PackedScene>("res://scenes/ui/OutroPanel.tscn").Instantiate<OutroPanel>();
		layer.AddChild(panel);
		panel.Show(title, text, GetViewport().GetVisibleRect().Size);
		panel.NewCampaignPressed += OnNewCampaignPressed;
		panel.RandomMissionsPressed += OnRandomMissionsPressed;
		panel.QuitPressed += () => GetTree().Quit();
	}

	private void OnNewCampaignPressed()
	{
		_narrativeController = null;
		_gameModeOverride = GameMode.Story;
		RegenerateLevel();
	}

	private void OnRandomMissionsPressed()
	{
		_narrativeController = null;
		_gameModeOverride = GameMode.Random;
		RegenerateLevel();
	}

	private void SpawnInterludePanel(string text, Action onDismiss)
	{
		var layer = new CanvasLayer { Layer = 13 };
		AddChild(layer);
		var panel = GD.Load<PackedScene>("res://scenes/ui/InterludePanel.tscn").Instantiate<InterludePanel>();
		layer.AddChild(panel);
		panel.Show(text, GetViewport().GetVisibleRect().Size, onDismiss);
	}

	private void SpawnChatWindow()
	{
		var layer = new CanvasLayer { Layer = 10 };
		AddChild(layer);
		_chatWindow = GD.Load<PackedScene>("res://scenes/ui/ChatWindowNode.tscn").Instantiate<ChatWindowNode>();
		layer.AddChild(_chatWindow);
		var viewportSize = GetViewport().GetVisibleRect().Size;
		const float chatHeight = 54f;
		const float sidePad = 8f;
		const float bottomPad = 8f;
		_chatWindow.Position = new Vector2(sidePad, viewportSize.Y - chatHeight - bottomPad);
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
		_systemActionMenu.HideAll();
		ShowEndSequence("Mission Complete", _activeCondition.EndDescription, won: true);
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
			_systemActionMenu.HideAll();
			ShowEndSequence("Defeated", "The marked system has fallen. The mission is lost.", won: false);
			return;
		}

		if (_systems.Any(s => s.IsPlayerOwned))
			return;

		_endConditionReached = true;
		_selectionPanel.Hide();
		_aiSystemPanel.Hide();
		_systemActionMenu.HideAll();
		var description = _endStateCfg.DefeatDescriptions[_rng.Next(_endStateCfg.DefeatDescriptions.Length)];
		ShowEndSequence("Defeated", description, won: false);
	}

	private void ShowEndSequence(string title, string description, bool won)
	{
		_narrativeController?.OnMissionComplete(
			won,
			_systems.Count(s => s.IsPlayerOwned),
			_systems.Count(s => s.IsAiOwned),
			_systems.Count(s => s.IsAiOwned && s.HasFleet));
		_fadeOverlay.MouseFilter = Control.MouseFilterEnum.Stop;
		_camera.PanTo(ComputeMapCenter(), _endStateCfg.EndStateSeconds);
		StartFadeOut(_fadeOutSeconds);
		_notificationPanel.Show(
			title,
			description,
			_endStateCfg.EndStateSeconds,
			GetViewport().GetVisibleRect().Size,
			onDismiss: OnEndSequenceDismissed,
			allowEarlyDismiss: false
		);
	}

	private void StartFadeOut(float durationSeconds)
	{
		var tween = CreateTween();
		tween.TweenProperty(_fadeOverlay, "modulate", new Color(1f, 1f, 1f, 1f), durationSeconds)
			.SetTrans(Tween.TransitionType.Linear);
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
			if (_systems.Any(s => s.OwnerPlayer == _targetPlayerOwner && s.HasFleet))
				return false;
		}

		if (condition.DefendObjectiveSystem == true)
		{
			if (_defendSystemIndex < 0 || !_systems[_defendSystemIndex].IsPlayerOwned)
				return false;
		}

		return true;
	}

	private void OnCountdownExpired()
	{
		if (_endConditionReached)
			return;
		_endConditionReached = true;
		_selectionPanel.Hide();
		_aiSystemPanel.Hide();
		_systemActionMenu.HideAll();
		ShowEndSequence("Time Expired", _activeCondition?.TimeoutMessage ?? "The mission clock has run out.", won: false);
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
		_drag = DragState.None;
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
		_pendingInterlude = null;
		_chatWindow = null;
		_camera = null!;
		_systemActionMenu = null!;
		_aiController = null!;
		_lastSystemClickTime = double.MinValue;
		_lastClickedSystemIndex = -1;
		_selectedFleetSlot = -1;
		_rerouteTargets.Clear();
		_rerouteArrows.Clear();
		_isPickingRerouteTarget = false;
		_rerouteSourceIndex = null;
		_rerouteArrowScene = null!;
		_upgradeCfg = null!;
		_debugOverlay = null!;
		_activeTransits.Clear();
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
			var permanent = (FogState)Math.Max((int)state, (int)_systems[i].FogState);
			_systems[i].SetFogState(permanent, _fogClearSeconds);
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

			Func<float, Action> arrivalCallback = fleetCount => () =>
			{
				_activeTransits.RemoveAll(t => t.Node == transit);
				ResolvePlayerTransitArrival(ti, fleetCount);
			};

			transit.Launch(fromEdge, toEdge, _ghostFleetOutline, _transitDurationSeconds, arrivalCallback(f));

			RegisterTransit(new ActiveTransit
			{
				FromIndex = si,
				ToIndex = ti,
				Owner = SystemOwner.Player,
				Fleet = f,
				LaunchTimeSec = Time.GetTicksMsec() / 1000.0,
				TotalDurationSec = _transitDurationSeconds,
				Node = transit,
				ToWorldPos = toEdge,
				ArrivalCallback = arrivalCallback
			});

			fogUpdateNeeded = true;
		}

		if (fogUpdateNeeded)
			UpdateFogOfWar();
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

	private bool AreConnected(int a, int b)
	{
		var edge = a < b ? (a, b) : (b, a);
		return _routeSet.Contains(edge);
	}

	private Color? SharedOwnerColor(int fromIndex, int toIndex)
	{
		var fromOwner = _systems[fromIndex].OwnerPlayer;
		var toOwner = _systems[toIndex].OwnerPlayer;
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

	private Vector2 ComputeMapCenter()
	{
		var sum = Vector2.Zero;
		foreach (var system in _systems)
			sum += system.GlobalPosition;
		return sum / _systems.Count;
	}

	private static Vector2 EdgeToward(Vector2 origin, Vector2 target, float radius)
		=> origin + (target - origin).Normalized() * radius;
}
