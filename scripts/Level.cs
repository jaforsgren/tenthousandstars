using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tts;

[Tool]
public partial class Level : Node2D
{
	private const int UnsetSeed = 0;

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

	private HashSet<(int, int)> _routeSet = [];
	private bool _isDragging;
	private int _draggingFromIndex = -1;
	private Vector2 _dragWorldPos;
	private float _ghostFleetRadius;
	private Color _ghostFleetFill;
	private Color _ghostFleetOutline;
	private float _ghostFleetOutlineWidth;

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
		Build(LevelGenerator.Generate(_rng, genCfg));
	}

	private void Build(LevelData data)
	{
		var sysCfg = ConfigLoader.Load<SystemConfig>("res://config/system.json");
		_ghostFleetRadius = sysCfg.FleetCircleRadius;
		_ghostFleetFill = sysCfg.FleetFill.ToColor();
		_ghostFleetOutline = sysCfg.FleetOutline.ToColor();
		_ghostFleetOutlineWidth = sysCfg.FleetOutlineWidth;
		_routeSet = new HashSet<(int, int)>(data.Routes);

		SpawnRoutes(data);
		SpawnSystems(data);
		SpawnCamera(data);
	}

	private void Clear()
	{
		foreach (var child in GetChildren())
			child.QueueFree();
		_systems.Clear();
		_routeSet.Clear();
		_isDragging = false;
		_draggingFromIndex = -1;
	}

	private void SpawnRoutes(LevelData data)
	{
		foreach (var (from, to) in data.Routes)
		{
			var route = new RouteNode();
			AddChild(route);
			route.Initialize(data.Systems[from].Position, data.Systems[to].Position);
		}
	}

	private void SpawnSystems(LevelData data)
	{
		foreach (var systemData in data.Systems)
		{
			var system = new SystemNode();
			system.Position = systemData.Position;
			AddChild(system);
			system.Initialize(systemData.Planets, systemData.Owner);
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
		else if (@event is InputEventMouseMotion && _isDragging)
		{
			_dragWorldPos = GetGlobalMousePosition();
			QueueRedraw();
			GetViewport().SetInputAsHandled();
		}
	}

	private void HandleMouseButton(InputEventMouseButton e)
	{
		if (e.ButtonIndex != MouseButton.Left)
			return;

		if (e.Pressed)
			TryBeginFleetDrag();
		else if (_isDragging)
			EndFleetDrag();
	}

	private void TryBeginFleetDrag()
	{
		var worldPos = GetGlobalMousePosition();
		for (var i = 0; i < _systems.Count; i++)
		{
			if (!_systems[i].HasFleet || !_systems[i].ContainsFleetAt(worldPos))
				continue;

			_draggingFromIndex = i;
			_dragWorldPos = worldPos;
			_isDragging = true;
			_systems[i].SetSelected(true);
			GetViewport().SetInputAsHandled();
			return;
		}
	}

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

			var ships = _systems[_draggingFromIndex].TakeFleet();
			_systems[i].ReceiveFleet(ships);
			resolved = true;
			break;
		}

		if (!resolved)
			_systems[_draggingFromIndex].SetSelected(false);

		_isDragging = false;
		_draggingFromIndex = -1;
		QueueRedraw();
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
