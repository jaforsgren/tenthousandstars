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
	private LoreConfig _loreConfig = null!;
	private SelectionPanel _selectionPanel = null!;
	private NotificationPanel _notificationPanel = null!;
	private EndStateConfig _endStateCfg = null!;
	private EndCondition? _activeCondition;
	private bool _endConditionReached;

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
		var seed = PreviewSeed != UnsetSeed ? PreviewSeed : levelCfg.DefaultPreviewSeed;
		Build(LevelGenerator.Generate(new Random(seed), genCfg));
	}

	private void GenerateRuntime()
	{
		var genCfg = ConfigLoader.Load<LevelGeneratorConfig>("res://config/level_generator.json");
		var data = LevelGenerator.Generate(_rng, genCfg);
		Build(data);
		UpdateFogOfWar();
		AssignLoreSeeds(data);
		_endStateCfg = ConfigLoader.Load<EndStateConfig>("res://config/end_states.json");
		_activeCondition = _endStateCfg.Conditions[_rng.Next(_endStateCfg.Conditions.Length)];
		SpawnSelectionPanel();
		SpawnNotificationPanel();
		ShowMissionBrief();
	}

	private void Build(LevelData data)
	{
		var sysCfg = ConfigLoader.Load<SystemConfig>("res://config/system.json");
		_ghostFleetRadius = sysCfg.FleetCircleRadius;
		_ghostFleetFill = sysCfg.FleetFill.ToColor();
		_ghostFleetOutline = sysCfg.FleetOutline.ToColor();
		_ghostFleetOutlineWidth = sysCfg.FleetOutlineWidth;
		_defenderBonus = ConfigLoader.Load<CombatConfig>("res://config/combat.json").DefenderBonus;
		_routeSet = new HashSet<(int, int)>(data.Routes);

		SpawnRoutes(data);
		SpawnSystems(data);
		SpawnCamera(data);
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

	private void SpawnSelectionPanel()
	{
		var layer = new CanvasLayer { Layer = 10 };
		AddChild(layer);
		_selectionPanel = new SelectionPanel();
		layer.AddChild(_selectionPanel);
	}

	private void SpawnNotificationPanel()
	{
		var layer = new CanvasLayer { Layer = 11 };
		AddChild(layer);
		_notificationPanel = new NotificationPanel();
		layer.AddChild(_notificationPanel);
	}

	private void ShowMissionBrief()
	{
		_notificationPanel.Show(
			"Mission",
			_activeCondition!.Description,
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
		_notificationPanel.Show(
			"Mission Complete",
			_activeCondition.EndDescription,
			_endStateCfg.EndStateSeconds,
			GetViewport().GetVisibleRect().Size,
			onDismiss: RegenerateLevel,
			allowEarlyDismiss: false
		);
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
		_routeNodes.Clear();
		_isDragging = false;
		_hasDragCandidate = false;
		_draggingFromIndex = -1;
		_dragCandidateIndex = -1;
		_selectionPanel = null!;
		_notificationPanel = null!;
		_activeCondition = null;
		_endConditionReached = false;
	}

	private void SpawnRoutes(LevelData data)
	{
		foreach (var (from, to) in data.Routes)
		{
			var route = new RouteNode();
			AddChild(route);
			route.Initialize(data.Systems[from].Position, data.Systems[to].Position);
			_routeNodes.Add((from, to, route));
		}
	}

	private void UpdateFogOfWar()
	{
		var scoutedByPlayer = new HashSet<int>();
		for (var i = 0; i < _systems.Count; i++)
		{
			if (!_systems[i].IsPlayerOwned)
				continue;
			foreach (var (from, to) in _routeSet)
			{
				if (from == i) scoutedByPlayer.Add(to);
				else if (to == i) scoutedByPlayer.Add(from);
			}
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
			_systems[i].SetFogState(state);
		}

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

			routeNode.SetFogState(routeState);
		}
	}

	private void SpawnSystems(LevelData data)
	{
		foreach (var systemData in data.Systems)
		{
			var system = new SystemNode();
			system.Position = systemData.Position;
			AddChild(system);
			system.Initialize(systemData.Planets, systemData.Owner, systemData.InitialFleet);
			_systems.Add(system);
		}
	}

	private void SpawnCamera(LevelData data)
	{
		if (Engine.IsEditorHint())
			return;

		var playerSystem = data.Systems.FirstOrDefault(s => s.Owner == SystemOwner.Player);
		var camera = new CameraController();
		AddChild(camera);
		camera.MakeCurrent();
		camera.FocusOn(playerSystem?.Position ?? Vector2.Zero);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Engine.IsEditorHint())
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
			if (!_systems[i].IsPlayerOwned || !_systems[i].HasFleet || !_systems[i].ContainsFleetAt(_pressWorldPos))
				continue;

			_dragCandidateIndex = i;
			_hasDragCandidate = true;
			GetViewport().SetInputAsHandled();
			return;
		}
	}

	private void HandleClick(Vector2 worldPos)
	{
		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].FogState == FogState.Hidden)
				continue;
			if (_systems[i].HasFleet && _systems[i].ContainsFleetAt(worldPos))
			{
				ShowFleetInfo(i);
				return;
			}
		}

		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].FogState == FogState.Hidden)
				continue;
			var planetIndex = _systems[i].PlanetIndexAt(worldPos);
			if (planetIndex.HasValue)
			{
				ShowPlanetInfo(i, planetIndex.Value);
				return;
			}
		}

		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].FogState == FogState.Hidden)
				continue;
			if (_systems[i].ContainsSystemAt(worldPos))
			{
				ShowSystemInfo(i);
				return;
			}
		}

		_selectionPanel.Hide();
	}

	private void ShowFleetInfo(int systemIndex)
	{
		var pool = _systems[systemIndex].IsPlayerOwned ? _loreConfig.PlayerFleet : _loreConfig.NeutralFleet;
		var seed = _fleetLoreSeeds[systemIndex];
		var title = Pick(pool.Titles, seed);
		var description = Pick(pool.Descriptions, seed);
		_selectionPanel.ShowAt($"Fleet — {title}", description, GetViewport().GetVisibleRect().Size);
	}

	private void ShowSystemInfo(int systemIndex)
	{
		var seed = _systemLoreSeeds[systemIndex];
		var title = Pick(_loreConfig.System.Titles, seed);
		var description = Pick(_loreConfig.System.Descriptions, seed);
		_selectionPanel.ShowAt(title, description, GetViewport().GetVisibleRect().Size);
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

			var attacker = _systems[_draggingFromIndex];
			var target = _systems[i];
			var attackerFleet = attacker.TakeFleet();

			if (target.Owner == attacker.Owner)
			{
				target.AddFleet(attackerFleet);
			}
			else
			{
				var result = CombatResolver.Resolve(attackerFleet, target.Ships, _defenderBonus);
				if (result.AttackerWins)
					target.Capture(result.AttackerRemainder, attacker.Owner);
				else
					target.SustainDefense(result.DefenderRemainder);
			}

			resolved = true;
			break;
		}

		if (!resolved)
			_systems[_draggingFromIndex].SetSelected(false);

		_isDragging = false;
		_draggingFromIndex = -1;
		QueueRedraw();

		if (resolved)
		{
			UpdateFogOfWar();
			CheckEndCondition();
		}
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
}
