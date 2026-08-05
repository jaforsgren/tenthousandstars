using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Tts.Ai;
using Tts.Config;
using Tts.Utils;

namespace Tts.Level;

// Builds a LevelData from an authored MapDefinition (config/maps/*.json) instead
// of procedural generation. Pure/no engine runtime so it is unit-testable.
public static class MapBuilder
{
    public static LevelData Build(MapDefinition map, Random rng, AiNamingConfig aiNamingCfg)
    {
        if (map.Systems.Length == 0)
            throw new ArgumentException("Map contains no systems.");

        if (map.Systems.Count(s => s.Owner == SystemOwner.Player) != 1)
            throw new ArgumentException("Map must contain exactly one player-owned system.");

        foreach (var route in map.Routes)
        {
            if (route.From < 0 || route.From >= map.Systems.Length
                || route.To < 0 || route.To >= map.Systems.Length)
                throw new ArgumentException($"Route ({route.From}, {route.To}) references an out-of-range system.");
        }

        var systems = map.Systems
            .Select(s => new SystemData(
                new Vector2(s.Position.X, s.Position.Y),
                s.Planets ?? [],
                s.Owner,
                s.InitialFleet,
                s.Name,
                s.Description))
            .ToList();

        var routes = map.Routes
            .Select(r => GraphUtils.NormalizedEdge(r.From, r.To))
            .Distinct()
            .ToList();

        var aiPlayers = BuildAiPlayers(rng, map.Systems, aiNamingCfg);
        return new LevelData(systems, routes, aiPlayers);
    }

    private static List<AiPlayerData> BuildAiPlayers(Random rng, MapSystem[] systems, AiNamingConfig aiNamingCfg)
    {
        var dispositions = Enum.GetValues<AiDisposition>();
        var aiOwners = systems
            .Select(s => s.Owner)
            .Where(o => o.IsAi())
            .Distinct()
            .OrderBy(o => o)
            .ToList();

        var aiPlayers = new List<AiPlayerData>(aiOwners.Count);
        foreach (var owner in aiOwners)
        {
            var disposition = systems
                .FirstOrDefault(s => s.Owner == owner && s.Disposition.HasValue)?
                .Disposition ?? dispositions[rng.Next(dispositions.Length)];
            aiPlayers.Add(AiNaming.GenerateAiPlayer(aiNamingCfg, owner, disposition, rng));
        }
        return aiPlayers;
    }
}