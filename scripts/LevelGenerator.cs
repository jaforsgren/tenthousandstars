using Godot;
using System;
using System.Collections.Generic;

namespace Tts;

public record SystemData(Vector2 Position, IReadOnlyList<Planet> Planets);
public record LevelData(IReadOnlyList<SystemData> Systems, IReadOnlyList<(int From, int To)> Routes);

public static class LevelGenerator
{
	public static LevelData Generate(Random rng, LevelGeneratorConfig cfg, int viewportWidth = 480, int viewportHeight = 720)
	{
		var count = rng.Next(cfg.MinSystems, cfg.MaxSystems + 1);
		var systems = PlaceSystems(rng, count, viewportWidth, viewportHeight, cfg);
		var routes = BuildRoutes(rng, systems, cfg);
		return new LevelData(systems, routes);
	}

	private static IReadOnlyList<SystemData> PlaceSystems(Random rng, int count, int width, int height, LevelGeneratorConfig cfg)
	{
		var positions = new List<Vector2>();
		var attempts = cfg.MaxPlacementAttempts;

		while (positions.Count < count && attempts-- > 0)
		{
			var candidate = new Vector2(
				cfg.Margin + (float)rng.NextDouble() * (width - cfg.Margin * 2f),
				cfg.Margin + (float)rng.NextDouble() * (height - cfg.Margin * 2f)
			);
			if (IsWellSpaced(candidate, positions, cfg.MinSpacing))
				positions.Add(candidate);
		}

		return positions.ConvertAll(p => new SystemData(p, GeneratePlanets(rng, cfg)));
	}

	private static bool IsWellSpaced(Vector2 candidate, List<Vector2> placed, float minSpacing)
	{
		foreach (var pos in placed)
			if (pos.DistanceTo(candidate) < minSpacing) return false;
		return true;
	}

	private static IReadOnlyList<Planet> GeneratePlanets(Random rng, LevelGeneratorConfig cfg)
	{
		var count = rng.Next(0, 6);
		var planets = new List<Planet>(count);
		var angleStep = count > 0 ? Mathf.Tau / count : 0f;

		for (var i = 0; i < count; i++)
		{
			var orbit = cfg.MinOrbit + (float)rng.NextDouble() * (cfg.MaxOrbit - cfg.MinOrbit);
			var angle = angleStep * i + (float)(rng.NextDouble() * 0.4);
			var size = cfg.MinPlanetSize + (float)rng.NextDouble() * (cfg.MaxPlanetSize - cfg.MinPlanetSize);
			planets.Add(new Planet(cfg.PlanetProductionRate, orbit, angle, size));
		}

		return planets;
	}

	private static IReadOnlyList<(int, int)> BuildRoutes(Random rng, IReadOnlyList<SystemData> systems, LevelGeneratorConfig cfg)
	{
		var routes = new HashSet<(int, int)>();
		var connections = new int[systems.Count];

		// Minimum spanning tree guarantees full traversability
		var inTree = new HashSet<int> { 0 };
		while (inTree.Count < systems.Count)
		{
			var bestDist = float.MaxValue;
			var bestFrom = -1;
			var bestTo = -1;

			foreach (var from in inTree)
			{
				for (var to = 0; to < systems.Count; to++)
				{
					if (inTree.Contains(to)) continue;
					var dist = systems[from].Position.DistanceTo(systems[to].Position);
					if (dist < bestDist) { bestDist = dist; bestFrom = from; bestTo = to; }
				}
			}

			inTree.Add(bestTo);
			routes.Add(NormalizedEdge(bestFrom, bestTo));
			connections[bestFrom]++;
			connections[bestTo]++;
		}

		// Extra routes for variety, respecting the per-system connection cap
		var extras = new List<(int, int)>();
		for (var i = 0; i < systems.Count; i++)
			for (var j = i + 1; j < systems.Count; j++)
				if (!routes.Contains((i, j)) && connections[i] < cfg.MaxConnectionsPerSystem && connections[j] < cfg.MaxConnectionsPerSystem)
					extras.Add((i, j));

		extras.Sort((a, b) =>
			systems[a.Item1].Position.DistanceTo(systems[a.Item2].Position)
			.CompareTo(systems[b.Item1].Position.DistanceTo(systems[b.Item2].Position)));

		var extraCount = rng.Next(1, Math.Min(5, extras.Count + 1));
		for (var i = 0; i < extraCount && i < extras.Count; i++)
		{
			var (f, t) = extras[i];
			if (connections[f] >= cfg.MaxConnectionsPerSystem || connections[t] >= cfg.MaxConnectionsPerSystem) continue;
			routes.Add((f, t));
			connections[f]++;
			connections[t]++;
		}

		return [.. routes];
	}

	private static (int, int) NormalizedEdge(int a, int b) => a < b ? (a, b) : (b, a);
}
