using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Tts.Ai;
using Tts.Commitment;
using Tts.Config;
using Tts.Debug;
using Tts.Dialogue;
using Tts.Effects;
using Tts.Events;
using Tts.Fleet;
using Tts.Narrative;
using Tts.Nodes;
using Tts.Ui;
using Tts.Utils;

namespace Tts.Level;

[Tool]
public partial class Level : Node2D
{
	private struct DragState
	{
		public bool IsActive;
		public bool HasCandidate;
		public bool IsCapitolShip;
		public int FromIndex;
		public int CandidateIndex;
		public int FleetSlot;
		public Vector2 WorldPos;

		public static readonly DragState None = new() { FromIndex = -1, CandidateIndex = -1, FleetSlot = -1 };
	}

	private const int UnsetSeed = 0;
	private const float DragThreshold = 8f;
	private const float DoubleClickThresholdSeconds = 0.35f;
	private const float RerouteMinFleet = 1.0f;

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

	private static readonly Color CapitolGhostFill = new(1f, 0.8f, 0.1f, 0.9f);

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
	private LoreConfig _loreConfig = null!;
	private IReadOnlyList<AiPlayerData> _aiPlayers = [];
	private BarkConfig? _barkConfig;
	private CameraController _camera = null!;
	private AiController _aiController = null!;
	private GameController _gameController = null!;
	private LevelUi _levelUi = null!;
	private double _lastSystemClickTime = double.MinValue;
	private int _lastClickedSystemIndex = -1;
	private LevelConfig _levelCfg = null!;
	private readonly Dictionary<int, int> _rerouteTargets = [];
	private readonly Dictionary<int, RerouteArrowNode> _rerouteArrows = [];
	private bool _isPickingRerouteTarget;
	private int? _rerouteSourceIndex;
	private PackedScene _rerouteArrowScene = null!;
	private int _selectedFleetSlot = -1;
	private SpeedControlPanel _speedControlPanel = null!;
	private CountdownTimerNode _countdownTimer = null!;
	private DebugOverlay? _debugOverlay;
	private bool _debugRevealFog;
	private ScenarioController _scenarioController = null!;
	private CommitmentController _commitmentController = null!;
	private CommitmentConfig _commitmentConfig = null!;
	private CommitmentDialogueController _commitmentDialogue = null!;
	private PackedScene _commitmentDialogueScene = null!;
	private LevelEventController _levelEventController = null!;
	private EffectRegistry _effectRegistry = null!;
	private EffectDisplayPanel _effectDisplayPanel = null!;
	private readonly TransitSystem _transitSystem = new();

	private FogSystem? _fogSystem;
	private bool _endConditionReached;
	private int _objectiveSystemIndex = -1;
	private readonly HashSet<int> _encounterSystems = [];
	private readonly List<RouteNode> _highlightedRoutes = [];

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
		{
			GeneratePreview();
		}
		else
		{
			_speedControlPanel = GetNode<SpeedControlPanel>("%SpeedControlPanel");
			_countdownTimer = GetNode<CountdownTimerNode>("%CountdownTimerNode");
			GenerateRuntime();
		}
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || _endConditionReached)
			return;
		if (_rerouteTargets.Count > 0)
			ProcessReroutes();
	}

	public override void _Draw()
	{
		if (!_drag.IsActive) return;
		var fill = _drag.IsCapitolShip ? CapitolGhostFill : _ghostFleetFill;
		DrawCircle(_drag.WorldPos, _ghostFleetRadius, fill);
		DrawArc(_drag.WorldPos, _ghostFleetRadius, 0f, Mathf.Tau, 32, _ghostFleetOutline, _ghostFleetOutlineWidth);
	}

	private void GeneratePreview()
	{
		ClearForPreview();
		var levelCfg = ConfigLoader.Load<LevelConfig>("res://config/level.json");
		var genCfg = ConfigLoader.Load<LevelGeneratorConfig>("res://config/level_generator.json");
		var aiCfg = ConfigLoader.Load<AiConfig>("res://config/ai.json");
		var aiNamingCfg = ConfigLoader.Load<AiNamingConfig>("res://config/ai_naming.json");
		var sysCfg = ConfigLoader.Load<SystemConfig>("res://config/system.json");
		var seed = PreviewSeed != UnsetSeed ? PreviewSeed : levelCfg.DefaultPreviewSeed;
		Build(LevelGenerator.Generate(new Random(seed), genCfg, aiCfg, aiNamingCfg, sysCfg), sysCfg);
	}

	private void GenerateRuntime()
	{
		GameSpeed.Reset();
		var genCfg = ConfigLoader.Load<LevelGeneratorConfig>("res://config/level_generator.json");
		var aiCfg = ConfigLoader.Load<AiConfig>("res://config/ai.json");
		var aiNamingCfg = ConfigLoader.Load<AiNamingConfig>("res://config/ai_naming.json");
		var sysCfg = ConfigLoader.Load<SystemConfig>("res://config/system.json");

		_barkConfig = ConfigLoader.Load<BarkConfig>("res://config/barks.json");
		var data = LevelGenerator.Generate(_rng, genCfg, aiCfg, aiNamingCfg, sysCfg);
		Build(data, sysCfg);
		_aiPlayers = data.AiPlayers;

		var endStateCfg = ConfigLoader.Load<EndStateConfig>("res://config/end_states.json");

		if (GameSession.NarrativeController == null)
		{
			var gameMode = GameSession.GameModeOverride ?? ConfigLoader.Load<GameModeConfig>("res://config/game_mode.json").Mode;
			if (gameMode == GameMode.Story)
			{
				var nc = NarrativeController.Create(_rng);
				nc.StartCampaign();
				GameSession.NarrativeController = nc;
			}
		}

		EndCondition? activeCondition;
		MissionContext? missionContext = null;
		ScenarioDefinition[] pendingScenarios;

		var narrative = GameSession.NarrativeController;
		if (narrative != null && !narrative.IsCampaignComplete)
		{
			var primaryAi = _aiPlayers.Count > 0 ? _aiPlayers[_rng.Next(_aiPlayers.Count)] : null;
			if (primaryAi != null)
				narrative.UpdateEnemy(primaryAi);
			missionContext = narrative.GetNextMission();
			activeCondition = missionContext.Condition.ToEndCondition();
			pendingScenarios = missionContext.Scenarios;
		}
		else
		{
			activeCondition = endStateCfg.Conditions[_rng.Next(endStateCfg.Conditions.Length)];
			pendingScenarios = LoadRandomModeScenarios();
		}

		AssignLoreSeeds(data);
		AssignScenarios(pendingScenarios);
		SpawnCommitmentController();
		SpawnEventSystem();
		AssignEncounters();
		SpawnAiController(data, aiCfg);

		_levelUi = GetNode<LevelUi>("%LevelUi");
		var uiCfg = ConfigLoader.Load<UiConfig>("res://config/ui.json");
		_levelUi.Initialize(_camera, _systemRadius, uiCfg.ActionMenu);

		_gameController = new GameController();
		AddChild(_gameController);
		_gameController.GameEnded += () => _endConditionReached = true;
		_gameController.Initialize(
			activeCondition, endStateCfg, missionContext,
			_routeSet, _systems, _aiPlayers, _rng, _levelUi, _camera, _countdownTimer, _fadeOutSeconds, _commitmentDialogue);

		_objectiveSystemIndex = _gameController.ObjectiveSystemIndex;
		UpdateFog();

		SpawnDebugOverlay();
		_gameController.StartMission();
	}

	private void SpawnDebugOverlay()
	{
		if (!OS.IsDebugBuild()) return;
		_debugOverlay = new DebugOverlay();
		AddChild(_debugOverlay);
	}

	private void ToggleDebugFog()
	{
		_debugRevealFog = !_debugRevealFog;
		UpdateFog();
		DebugOverlay.Log($"Fog reveal: {(_debugRevealFog ? "ON" : "OFF")}");
	}

	private void Build(LevelData data, SystemConfig sysCfg)
	{
		var aiCfg = ConfigLoader.Load<AiConfig>("res://config/ai.json");
		_ghostFleetRadius = sysCfg.LabelHeight / 2f;
		_ghostFleetFill = sysCfg.FleetFill.ToColor();
		_ghostFleetOutline = sysCfg.FleetOutline.ToColor();
		_ghostFleetOutlineWidth = sysCfg.FleetOutlineWidth;
		_systemRadius = sysCfg.SystemRadius;
		_levelCfg = ConfigLoader.Load<LevelConfig>("res://config/level.json");
		_defenderBonus = _levelCfg.DefenderBonus;
		_fogEnabled = _levelCfg.FogEnabled;
		_fogClearSeconds = _levelCfg.FogClearSeconds;
		_fadeOutSeconds = _levelCfg.FadeOutSeconds;
		_transitDurationSeconds = _levelCfg.TransitDurationSeconds;
		_transitFleetScene = GD.Load<PackedScene>("res://scenes/fleet/TransitFleetNode.tscn");
		_combatEffectScene = GD.Load<PackedScene>("res://scenes/effects/CombatEffectNode.tscn");
		_rerouteArrowScene = GD.Load<PackedScene>("res://scenes/system/RerouteArrowNode.tscn");
		_routeSet = new HashSet<(int, int)>(data.Routes);
		_adjacency = GraphUtils.BuildAdjacency(data.Systems.Count, _routeSet);

		var camCfg = ConfigLoader.Load<CameraConfig>("res://config/camera.json");

		SpawnRoutes(data);
		SpawnSystems(data, aiCfg, sysCfg);
		SpawnCamera(data, camCfg);
		_fogSystem = new FogSystem(_systems, _routeNodes, _adjacency, _aiColors, _fogEnabled, _fogClearSeconds);
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

	private void SpawnEventSystem()
	{
		_effectRegistry = new EffectRegistry();
		var scenarioType = (LevelScenarioType)_rng.Next(4);
		_levelEventController = new LevelEventController(scenarioType, _commitmentConfig.EncounterSystemChance, _rng);

		_effectDisplayPanel = new EffectDisplayPanel();
		AddChild(_effectDisplayPanel);
		_effectRegistry.Changed += () => _effectDisplayPanel.Refresh(_effectRegistry.Effects);
	}

	private void AssignEncounters()
	{
		_encounterSystems.Clear();
		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].IsPlayerOwned) continue;
			if (_rng.NextDouble() < _commitmentConfig.EncounterSystemChance)
			{
				_encounterSystems.Add(i);
				_systems[i].SetEncounterMark(true);
			}
		}
	}

	private void AssignScenarios(ScenarioDefinition[] scenarios)
	{
		_scenarioController = new ScenarioController();
		_scenarioController.AssignScenarios(_systems, scenarios, _rng);
		for (var i = 0; i < _systems.Count; i++)
			_systems[i].SetScenarioBadge(_scenarioController.HasScenario(i));
	}

	private static ScenarioDefinition[] LoadRandomModeScenarios()
	{
		var cfg = ConfigLoader.Load<ScenarioConfig>("res://config/scenarios.json");
		return System.Array.FindAll(cfg.Scenarios, s => s.Criteria.MinMissionsPlayed == 0
			&& s.Criteria.MinMissionsWon == null
			&& s.Criteria.MinMissionsLost == null);
	}

	private void UpdateFog()
	{
		if (_fogSystem == null) return;
		if (_debugRevealFog)
		{
			_fogSystem.RevealAll();
			return;
		}
		var committed = new HashSet<int>();
		foreach (var c in _commitmentController.GetAllActive())
			if (c.Owner == SystemOwner.Player && !c.IsComplete && !c.IsInterrupted)
				committed.Add(c.SystemIndex);
		_fogSystem.Update(_objectiveSystemIndex, committed);
	}

	private void SpawnCommitmentController()
	{
		_commitmentConfig = ConfigLoader.Load<CommitmentConfig>("res://config/commitment.json");
		_commitmentController = new CommitmentController();
		AddChild(_commitmentController);
		_commitmentController.Initialize(
			_commitmentConfig,
			_systems,
			owner => _aiColors.GetValueOrDefault(owner, new Color(0.6f, 0.6f, 0.6f)),
			_systemRadius,
			_rng.Next());
		_commitmentController.CommitmentResolved += OnCommitmentResolved;

		_commitmentDialogueScene = GD.Load<PackedScene>("res://scenes/dialogue/CommitmentDialoguePanel.tscn");
		_commitmentDialogue = _commitmentDialogueScene.Instantiate<CommitmentDialogueController>();
		AddChild(_commitmentDialogue);
		_commitmentController.CommitmentResolved += _commitmentDialogue.OnCommitmentResolved;
	}

	private void OnCommitmentResolved(int systemIndex, int ownerInt, int intentInt, bool controlGained, float remainingStrength)
	{
		var owner = (SystemOwner)ownerInt;
		var target = _systems[systemIndex];

		if (controlGained)
		{
			if (owner == SystemOwner.Player)
			{
				target.Capture(remainingStrength, SystemOwner.Player);
				SpawnCombatEffect(systemIndex, attackerWon: true);
				if (_camera.IsFollowing)
					_camera.FollowSystem(target.GlobalPosition);
				ClearReroute(systemIndex);
			}
			else
			{
				var aiPlayer = _aiPlayers.FirstOrDefault(p => p.Owner == owner);
				if (aiPlayer != null)
				{
					target.Capture(remainingStrength, owner, aiPlayer, _aiColors[owner]);
					SpawnCombatEffect(systemIndex, attackerWon: true);
					ClearReroute(systemIndex);
				}
			}
		}
		else
		{
			SpawnCombatEffect(systemIndex, attackerWon: false);
		}

		UpdateFog();
		_gameController.EvaluateEndState();
	}

	private void SpawnAiController(LevelData data, AiConfig aiCfg)
	{
		_aiController = new AiController();
		AddChild(_aiController);
		_aiController.ActionTaken += OnAiActionTaken;
		_aiController.Initialize(
			_systems, _routeSet, _defenderBonus, data.AiPlayers, aiCfg,
			_commitmentConfig, _commitmentController,
			_rng, LaunchAiTransit, CommitAiOwnSystem);
	}

	private void OnAiActionTaken()
	{
		UpdateFog();
		_gameController.EvaluateEndState();
	}

	// Editor-only: resets scene nodes before regenerating preview layout.
	private void ClearForPreview()
	{
		foreach (var child in GetChildren())
			if (child.Name != "PersistentUI" && child.Name != "Background" && child.Name != "PostProcess"
				&& child.Name != "LevelUi" && child.Name != "CameraController")
				child.QueueFree();
		_systems.Clear();
		_routeSet.Clear();
		_adjacency.Clear();
		_routeNodes.Clear();
		_transitSystem.Clear();
		_fogSystem = null;
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
		_camera = GetNodeOrNull<CameraController>("CameraController") ?? AddCameraController();
		_camera.MakeCurrent();
		_camera.FocusOn(playerSystem?.Position ?? Vector2.Zero, camCfg.StartZoom);
	}

	private CameraController AddCameraController()
	{
		var camera = new CameraController();
		AddChild(camera);
		return camera;
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
			LaunchTransit(sourceIndex, targetIndex, fleet, SystemOwner.Player, _ghostFleetOutline,
				(toIdx, f) => ResolvePlayerTransitArrival(toIdx, f));

			fogUpdateNeeded = true;
		}

		if (fogUpdateNeeded)
			UpdateFog();
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

	private void SetPathHighlight(List<int>? path)
	{
		foreach (var r in _highlightedRoutes)
			if (IsInstanceValid(r)) r.SetPathHighlight(false);
		_highlightedRoutes.Clear();

		if (path == null || path.Count < 2) return;
		for (var i = 0; i < path.Count - 1; i++)
		{
			var route = FindRouteNode(path[i], path[i + 1]);
			if (route == null) continue;
			route.SetPathHighlight(true);
			_highlightedRoutes.Add(route);
		}
	}

	private RouteNode? FindRouteNode(int a, int b)
	{
		var (min, max) = a < b ? (a, b) : (b, a);
		foreach (var (from, to, node) in _routeNodes)
			if (from == min && to == max) return node;
		return null;
	}

	private static Vector2 EdgeToward(Vector2 origin, Vector2 target, float radius)
		=> origin + (target - origin).Normalized() * radius;
}
