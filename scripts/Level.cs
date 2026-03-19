using Godot;
using System;
using System.Collections.Generic;

namespace Tts;

public partial class Level : Node2D
{
	private readonly Random _rng = new();
	private readonly List<SystemNode> _systems = [];

	public override void _Ready()
	{
		var data = LevelGenerator.Generate(_rng);
		SpawnRoutes(data);
		SpawnSystems(data);
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
