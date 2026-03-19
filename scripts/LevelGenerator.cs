using Godot;
using System;
using System.Collections.Generic;

namespace Tts;

public record SystemData(Vector2 Position, IReadOnlyList<Planet> Planets);
public record LevelData(IReadOnlyList<SystemData> Systems, IReadOnlyList<(int From, int To)> Routes);

public static class LevelGenerator
{
	private const int MinSystems = 6;
	private const int MaxSystems = 12;
	private const float MinSpacing = 130f;
	private const float Margin = 70f;
	private const float PlanetProductionRate = 0.2f;
	private const float MinOrbit = 14f;
	private const float MaxOrbit = 38f;
	private const float MinPlanetSize = 4f;
	private const float MaxPlanetSize = 8f;
	private const int MaxPlacementAttempts = 500;
	private const int MaxConnectionsPerSystem = 4;

	public static LevelData Generate(Random rng, int viewportWidth = 480, int viewportHeight = 720)
	{
		var count = rng.Next(MinSystems, MaxSystems + 1);
		var systems = PlaceSystems(rng, count, viewportWidth, viewportHeight);
		var routes = BuildRoutes(rng, systems);
		return new LevelData(systems, routes);
	}

	private static IReadOnlyList<SystemData> PlaceSystems(Random rng, int count, int width, int height)
	{
		var positions = new List<Vector2>();
		var attempts = MaxPlacementAttempts;

		while (positions.Count < count && attempts-- > 0)
		{
			var candidate = new Vector2(
				Margin + (float)rng.NextDouble() * (width - Margin * 2f),
				Margin + (float)rng.NextDouble() * (height - Margin * 2f)
			);
			if (IsWellSpaced(candidate, positions))
				positions.Add(candidate);
		}

		return positions.ConvertAll(p => new SystemData(p, GeneratePlanets(rng)));
	}

	private static bool IsWellSpaced(Vector2 candidate, List<Vector2> placed)
	{
		foreach (var pos in placed)
			if (pos.DistanceTo(candidate) < MinSpacing) return false;
		return true;
	}

	private static IReadOnlyList<Planet> GeneratePlanets(Random rng)
	{
		var count = rng.Next(0, 6);
		var planets = new List<Planet>(count);
		var angleStep = count > 0 ? Mathf.Tau / count : 0f;

		for (var i = 0; i < count; i++)
		{
			var orbit = MinOrbit + (float)rng.NextDouble() * (MaxOrbit - MinOrbit);
			var angle = angleStep * i + (float)(rng.NextDouble() * 0.4);
			var size = MinPlanetSize + (float)rng.NextDouble() * (MaxPlanetSize - MinPlanetSize);
			planets.Add(new Planet(PlanetProductionRate, orbit, angle, size));
		}

		return planets;
	}

	private static IReadOnlyList<(int, int)> BuildRoutes(Random rng, IReadOnlyList<SystemData> systems)
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
				if (!routes.Contains((i, j)) && connections[i] < MaxConnectionsPerSystem && connections[j] < MaxConnectionsPerSystem)
					extras.Add((i, j));

		extras.Sort((a, b) =>
			systems[a.Item1].Position.DistanceTo(systems[a.Item2].Position)
			.CompareTo(systems[b.Item1].Position.DistanceTo(systems[b.Item2].Position)));

		var extraCount = rng.Next(1, Math.Min(5, extras.Count + 1));
		for (var i = 0; i < extraCount && i < extras.Count; i++)
		{
			var (f, t) = extras[i];
			if (connections[f] >= MaxConnectionsPerSystem || connections[t] >= MaxConnectionsPerSystem) continue;
			routes.Add((f, t));
			connections[f]++;
			connections[t]++;
		}

		return [.. routes];
	}

	private static (int, int) NormalizedEdge(int a, int b) => a < b ? (a, b) : (b, a);
}
