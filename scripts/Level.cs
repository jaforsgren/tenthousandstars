using Godot;
using System;
using System.Collections.Generic;

namespace Tts;

[Tool]
public partial class Level : Node2D
{
	private const int DefaultPreviewSeed = 42;

	[Export]
	public int PreviewSeed { get; set; } = DefaultPreviewSeed;

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
		Build(LevelGenerator.Generate(new Random(PreviewSeed)));
	}

	private void GenerateRuntime()
	{
		Build(LevelGenerator.Generate(_rng));
	}

	private void Build(LevelData data)
	{
		SpawnRoutes(data);
		SpawnSystems(data);
	}

	private void Clear()
	{
		foreach (var child in GetChildren())
			child.QueueFree();
		_systems.Clear();
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
			system.Initialize(systemData.Planets);
			_systems.Add(system);
		}
	}
}
