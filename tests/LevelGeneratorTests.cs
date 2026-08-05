using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Tts.Ai;
using Tts.Config;
using Tts.Level;

namespace Tts.Tests;

public class LevelGeneratorTests
{
    private static LevelGeneratorConfig MakeCfg(int minPlanets = 0, int maxPlanets = 6, int maxExtraRoutes = 5, int minSystems = 10, int maxSystems = 20)
    {
        return new LevelGeneratorConfig(
            MinSystems: minSystems,
            MaxSystems: maxSystems,
            MinSpacing: 200f,
            Margin: 140f,
            SpawnWidth: 1200f,
            SpawnHeight: 1800f,
            MinOrbit: 14f,
            MaxOrbit: 38f,
            MinPlanetSize: 1f,
            MaxPlanetSize: 4f,
            MinPlanets: minPlanets,
            MaxPlanets: maxPlanets,
            MaxExtraRoutes: maxExtraRoutes,
            MaxPlacementAttempts: 500,
            MaxConnectionsPerSystem: 4,
            NeutralFleetMin: 2,
            NeutralFleetMax: 5,
            AiMinHopsFromPlayer: 3);
    }

    private static readonly AiConfig Ai = new(
        MinOpponents: 0,
        MaxOpponents: 0,
        ThinkIntervalSeconds: 1f,
        StrategicMinSpareShips: 5f,
        CautiousAttackChance: 0.2f,
        DispositionReinforceChance: new Dictionary<string, float>(),
        DispositionColors: new Dictionary<string, ColorData>());

    private static readonly AiNamingConfig Naming = new(
        DispositionDescriptions: DispositionMap(["desc"]),
        DispositionBarks: DispositionMap(["bark"]),
        DispositionAbbreviations: DispositionMap(["abbr"]),
        Names: [new AiFactionNameEntry("Noun", "Adjective")],
        Suffix: DispositionMap(["suffix"]));

    private static Dictionary<string, string[]> DispositionMap(string[] values)
    {
        return new Dictionary<string, string[]>
        {
            ["Aggressive"] = values,
            ["Strategic"] = values,
            ["Cautious"] = values,
            ["Dormant"] = values,
        };
    }

    private static readonly SystemConfig System = new(
        SystemRadius: 40f,
        FleetCircleGap: 2f,
        BaseProduction: 1f,
        PlanetProductionRate: 1f,
        LabelWidth: 80f,
        LabelHeight: 20f,
        SystemFill: new ColorData(1f, 1f, 1f, 1f),
        SystemOutline: new ColorData(1f, 1f, 1f, 1f),
        NeutralSystemOutline: new ColorData(1f, 1f, 1f, 1f),
        SystemOutlineWidth: 2f,
        SunRadiusRatio: 0.5f,
        PlanetOrbitSpeed: 1f,
        PlanetGradients: [],
        FleetFill: new ColorData(1f, 1f, 1f, 1f),
        FleetOutline: new ColorData(1f, 1f, 1f, 1f),
        FleetOutlineWidth: 1f,
        NeutralFleetFill: new ColorData(1f, 1f, 1f, 1f),
        NeutralFleetOutline: new ColorData(1f, 1f, 1f, 1f),
        CapitolFill: new ColorData(1f, 1f, 1f, 1f),
        SystemTexturePaths: [],
        SystemTextureScales: []);

    [Theory]
    [InlineData(0, 6)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    public void GeneratePlanets_RespectsConfiguredCountBounds(int minPlanets, int maxPlanets)
    {
        var cfg = MakeCfg(minPlanets: minPlanets, maxPlanets: maxPlanets);

        for (var seed = 0; seed < 50; seed++)
        {
            var level = LevelGenerator.Generate(new Random(seed), cfg, Ai, Naming, System);
            foreach (var system in level.Systems)
                Assert.InRange(system.Planets.Count, minPlanets, maxPlanets - 1);
        }
    }

    [Fact]
    public void Generate_RouteCount_RespectsMaxExtraRoutes()
    {
        var cfg = MakeCfg(maxExtraRoutes: 2, minSystems: 20, maxSystems: 20);

        for (var seed = 0; seed < 20; seed++)
        {
            var level = LevelGenerator.Generate(new Random(seed), cfg, Ai, Naming, System);
            var extraRoutes = level.Routes.Count - (level.Systems.Count - 1);
            Assert.InRange(extraRoutes, 1, cfg.MaxExtraRoutes);
        }
    }

    [Fact]
    public void Generate_AlwaysProducesConnectedGraph()
    {
        var level = LevelGenerator.Generate(new Random(7), MakeCfg(), Ai, Naming, System);
        var routes = level.Routes.Select(r => ((int, int))(r.From, r.To)).ToList();
        var hops = GraphUtils.BfsHopDistances(0, level.Systems.Count, routes);
        Assert.All(hops, d => Assert.True(d >= 0));
        Assert.True(level.Routes.Count >= level.Systems.Count - 1);
    }
}
