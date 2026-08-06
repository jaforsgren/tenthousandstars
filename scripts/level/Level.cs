using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Tts.Ai;
using Tts.Commitment;
using Tts.Config;
using Tts.Debug;
using Tts.Effects;
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

	private float _ghostFleetRadius;
	private Color _ghostFleetFill;
	private Color _ghostFleetOutline;
	private Color _capitolFill;
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
	private Dictionary<string, string[]> _lorePools = null!;
	private IReadOnlyList<AiPlayerData> _aiPlayers = [];
	private IReadOnlyList<SystemData> _systemData = [];
	private Dictionary<string, string[]> _barkPools = [];
	private CameraController _camera = null!;
	private AiController _aiController = null!;
	private GameController _gameController = null!;
	private LevelUi _levelUi = null!;
	private double _lastSystemClickTime = double.MinValue;
	private int _lastClickedSystemIndex = -1;
	private LevelConfig _levelCfg = null!;
	private UiConfig _uiCfg = null!;
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
	private ScenarioRegistry _scenarioRegistry = null!;
	private CommitmentController _commitmentController = null!;
	private CommitmentConfig _commitmentConfig = null!;
	private NarrativePanel _narrativePanel = null!;
	private PackedScene _narrativePanelScene = null!;
	private EffectRegistry _effectRegistry = null!;
	private EffectDisplayPanel _effectDisplayPanel = null!;
	private readonly TransitSystem _transitSystem = new();

	private FogSystem? _fogSystem;
	private bool _endConditionReached;
	private int _objectiveSystemIndex = -1;
	private readonly List<RouteNode> _highlightedRoutes = [];
	private MapEvent[] _mapEvents = [];
	private MapEventController _mapEventController = null!;

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
		var fill = _drag.IsCapitolShip ? _capitolFill.WithAlpha(0.9f) : _ghostFleetFill;
		DrawCircle(_drag.WorldPos, _ghostFleetRadius, fill);
		DrawArc(_drag.WorldPos, _ghostFleetRadius, 0f, Mathf.Tau, 32, _ghostFleetOutline, _ghostFleetOutlineWidth);
	}

	private void GeneratePreview()
	{
		ClearForPreview();
		var levelCfg = ConfigLoader.Load<LevelConfig>("res://config/level.json");
		var cfg = GeneratorConfigLoader.Load();
		var seed = PreviewSeed != UnsetSeed ? PreviewSeed : levelCfg.DefaultPreviewSeed;
		Build(LevelGenerator.Generate(new Random(seed), cfg.Generator, cfg.Ai, cfg.AiNaming, cfg.System), cfg.Ai, cfg.System, levelCfg);
	}

	private void GenerateRuntime()
	{
		GameSpeed.Reset();
		var cfg = GeneratorConfigLoader.Load();

		_barkPools = LoadBarkPools("res://yarn/barks.yarn");
		var levelCfg = ConfigLoader.Load<LevelConfig>("res://config/level.json");
		var endStateCfg = LoadEndStateConfig();

		var campaign = ResolveCampaign();
		var data = BuildLevelData(campaign, cfg);
		Build(data, cfg.Ai, cfg.System, levelCfg);
		_aiPlayers = data.AiPlayers;
		_systemData = data.Systems;

		if (campaign != null && _aiPlayers.Count > 0)
			campaign.UpdateEnemyFaction(_aiPlayers[_rng.Next(_aiPlayers.Count)].FactionName);

		EndCondition? activeCondition;
		if (campaign != null && !campaign.IsCampaignComplete && !campaign.Current.IsTerminal)
			activeCondition = campaign.Current.ToEndCondition();
		else
			activeCondition = endStateCfg.Conditions[_rng.Next(endStateCfg.Conditions.Length)];

		var pendingScenarios = LoadSkirmishScenarios();

		AssignLoreSeeds(data);
		AssignScenarios(pendingScenarios);
		SpawnCommitmentController();
		SpawnMapEvents(campaign);
		SpawnEffectSystem();
		SpawnAiController(data, cfg.Ai);

		_levelUi = GetNode<LevelUi>("%LevelUi");
		_levelUi.Initialize(_camera, _systemRadius, _uiCfg.ActionMenu);

		_gameController = new GameController();
		AddChild(_gameController);
		_gameController.GameEnded += () => _endConditionReached = true;
		_gameController.Initialize(
			activeCondition, endStateCfg,
			_routeSet, _systems, _aiPlayers, _rng, _levelUi, _camera, _countdownTimer, _fadeOutSeconds, _narrativePanel);

		_objectiveSystemIndex = _gameController.ObjectiveSystemIndex;
		UpdateFog();

		SpawnDebugOverlay();
		_gameController.StartMission();
	}

	private CampaignController? ResolveCampaign()
	{
		if (GameSession.Campaign != null)
			return GameSession.Campaign;

		var gameMode = GameSession.GameModeOverride ?? ConfigLoader.Load<GameModeConfig>("res://config/game_mode.json").Mode;
		if (gameMode == GameMode.Story)
			GameSession.Campaign = CampaignController.Load(_rng);

		return GameSession.Campaign;
	}

	private LevelData BuildLevelData(CampaignController? campaign, GeneratorConfigs cfg)
	{
		var isMission = campaign != null && !campaign.IsCampaignComplete && !campaign.Current.IsTerminal;
		var mapName = isMission ? campaign!.Current.Map : null;

		var generationRng = isMission && campaign!.Current.LevelSeed.HasValue
			? new Random(campaign.Current.LevelSeed.Value)
			: _rng;

		if (mapName != null)
		{
			var mapPath = $"res://config/maps/{mapName}.json";
			if (ConfigLoader.Exists(mapPath))
			{
				var map = ConfigLoader.Load<MapDefinition>(mapPath);
				_mapEvents = map.Events ?? [];
				return MapBuilder.Build(map, generationRng, cfg.AiNaming);
			}
		}

		_mapEvents = [];
		return LevelGenerator.Generate(generationRng, cfg.Generator, cfg.Ai, cfg.AiNaming, cfg.System);
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

	private void Build(LevelData data, AiConfig aiCfg, SystemConfig sysCfg, LevelConfig levelCfg)
	{
		_ghostFleetRadius = sysCfg.LabelHeight / 2f;
		_ghostFleetFill = sysCfg.FleetFill.ToColor();
		_ghostFleetOutline = sysCfg.FleetOutline.ToColor();
		_capitolFill = sysCfg.CapitolFill.ToColor();
		_ghostFleetOutlineWidth = sysCfg.FleetOutlineWidth;
		_systemRadius = sysCfg.SystemRadius;
		_levelCfg = levelCfg;
		_defenderBonus = _levelCfg.DefenderBonus;
		_fogEnabled = _levelCfg.FogEnabled;
		_fogClearSeconds = _levelCfg.FogClearSeconds;
		_fadeOutSeconds = _levelCfg.FadeOutSeconds;
		_transitDurationSeconds = _levelCfg.TransitDurationSeconds;
		_transitFleetScene = GD.Load<PackedScene>(ScenePaths.TransitFleetNode);
		_combatEffectScene = GD.Load<PackedScene>(ScenePaths.CombatEffectNode);
		_rerouteArrowScene = GD.Load<PackedScene>(ScenePaths.RerouteArrowNode);
		_routeSet = new HashSet<(int, int)>(data.Routes);
		_adjacency = GraphUtils.BuildAdjacency(data.Systems.Count, _routeSet);

		var camCfg = ConfigLoader.Load<CameraConfig>("res://config/camera.json");
		_uiCfg = ConfigLoader.Load<UiConfig>("res://config/ui.json");

		SpawnRoutes(data, _uiCfg.RouteWidth);
		SpawnSystems(data, aiCfg, sysCfg);
		SpawnCamera(data, camCfg);
		_fogSystem = new FogSystem(_systems, _routeNodes, _adjacency, _aiColors, _fogEnabled, _fogClearSeconds);
	}

	private void AssignLoreSeeds(LevelData data)
	{
		_lorePools = YarnLinePool.Load("res://yarn/lore.yarn");
		var loreRng = new Random();

		_systemLoreSeeds.Clear();
		_fleetLoreSeeds.Clear();

		foreach (var systemData in data.Systems)
		{
			_systemLoreSeeds.Add(loreRng.Next());
			_fleetLoreSeeds.Add(loreRng.Next());
		}
	}

	private void SpawnEffectSystem()
	{
		_effectRegistry = new EffectRegistry();

		_effectDisplayPanel = new EffectDisplayPanel();
		AddChild(_effectDisplayPanel);
		_effectRegistry.Changed += () => _effectDisplayPanel.Refresh(_effectRegistry.Effects);
	}

	private void AssignScenarios(ScenarioDefinition[] scenarios)
	{
		_scenarioRegistry = new ScenarioRegistry();
		_scenarioRegistry.AssignScenarios(_systems, scenarios, _rng);
		for (var i = 0; i < _systems.Count; i++)
			_systems[i].SetScenarioBadge(_scenarioRegistry.HasScenario(i));
	}

	private static ScenarioDefinition[] LoadSkirmishScenarios()
	{
		var cfg = ConfigLoader.Load<ScenarioConfig>("res://config/scenarios.json");
		var scenarioPools = LoadScenarioPools();
		var enriched = cfg.Scenarios.Select(s => s with
		{
			Title     = YarnLinePool.GetFirst(scenarioPools, $"{s.Id}_title")      ?? s.Id,
			IntroText = YarnLinePool.GetFirst(scenarioPools, $"{s.Id}_intro_text") ?? "",
			Stages    = s.Stages.Select(stage => stage with
			{
				Text = YarnLinePool.GetText(scenarioPools, $"{s.Id}_{stage.DependsOn ?? "root"}")
			}).ToArray()
		});
		return enriched.Where(s =>
			s.Criteria.MinMissionsPlayed == 0
			&& s.Criteria.MinMissionsWon == null
			&& s.Criteria.MinMissionsLost == null).ToArray();
	}

	private static Dictionary<string, string[]> LoadScenarioPools()
	{
		var scenarioPoolFiles = new[] { "the_relay_chain", "survivor_enclave", "the_ghost_fleet" };
		var scenarioPools = new Dictionary<string, string[]>(StringComparer.Ordinal);
		foreach (var file in scenarioPoolFiles)
		{
			var filePools = YarnLinePool.Load($"res://yarn/scenarios/{file}.yarn");
			foreach (var kv in filePools) scenarioPools[kv.Key] = kv.Value;
		}
		return scenarioPools;
	}

	private static EndStateConfig LoadEndStateConfig()
	{
		var cfg = ConfigLoader.Load<EndStateConfig>("res://config/end_states.json");
		var pools = YarnLinePool.Load("res://yarn/end_states.yarn");
		var defeatDescriptions = YarnLinePool.GetPool(pools, "defeat_descriptions");
		var enrichedConditions = cfg.Conditions.Select(c => c with
		{
			Description    = YarnLinePool.GetFirst(pools, $"{c.Id}_brief")   ?? c.Description,
			EndDescription = YarnLinePool.GetFirst(pools, $"{c.Id}_end")     ?? c.EndDescription,
			TimeoutMessage = YarnLinePool.GetFirst(pools, $"{c.Id}_timeout") ?? c.TimeoutMessage
		}).ToArray();
		return cfg with { DefeatDescriptions = defeatDescriptions, Conditions = enrichedConditions };
	}

	private static Dictionary<string, string[]> LoadBarkPools(string yarnPath)
		=> YarnLinePool.Load(yarnPath);

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
			if (c.Owner == SystemOwner.Player && c.IsActive)
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

		_narrativePanelScene = GD.Load<PackedScene>(ScenePaths.NarrativePanel);
		_narrativePanel = _narrativePanelScene.Instantiate<NarrativePanel>();
		AddChild(_narrativePanel);
		_commitmentController.CommitmentResolved += _narrativePanel.OnCommitmentResolved;
	}

	private void SpawnMapEvents(CampaignController? campaign)
	{
		_mapEventController = new MapEventController();
		AddChild(_mapEventController);
		_mapEventController.Initialize(
			_mapEvents,
			_narrativePanel,
			campaign?.BuildVars() ?? new Dictionary<string, string>());
		_mapEventController.NotifyMissionStart();
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
				_mapEventController?.NotifyPlayerConquer();
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

	private void SpawnRoutes(LevelData data, float routeWidth)
	{
		foreach (var (from, to) in data.Routes)
		{
			var fromPos = data.Systems[from].Position;
			var toPos = data.Systems[to].Position;
			var route = new RouteNode();
			AddChild(route);
			route.Initialize(
				EdgeToward(fromPos, toPos, _systemRadius),
				EdgeToward(toPos, fromPos, _systemRadius),
				routeWidth);
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
		var edge = GraphUtils.NormalizedEdge(a, b);
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
		var (min, max) = GraphUtils.NormalizedEdge(a, b);
		foreach (var (from, to, node) in _routeNodes)
			if (from == min && to == max) return node;
		return null;
	}

	private static Vector2 EdgeToward(Vector2 origin, Vector2 target, float radius)
		=> origin + (target - origin).Normalized() * radius;
}
