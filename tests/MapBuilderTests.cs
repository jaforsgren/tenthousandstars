using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Tts.Ai;
using Tts.Config;
using Tts.Level;
using Tts.Utils;

namespace Tts.Tests;

public class MapBuilderTests
{
    private static AiNamingConfig Naming => new(
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

    private static MapDefinition MakeMap() => new(
        Systems:
        [
            new MapSystem(new MapPoint(600, 1500), SystemOwner.Player, InitialFleet: 5, Name: "Vrehl", Description: "The bridgehead."),
            new MapSystem(new MapPoint(420, 1050), SystemOwner.Ai1, InitialFleet: 3, Disposition: AiDisposition.Cautious),
            new MapSystem(new MapPoint(780, 1050), SystemOwner.None, InitialFleet: 2)
        ],
        Routes:
        [
            new MapRoute(0, 1),
            new MapRoute(0, 2),
            new MapRoute(1, 2)
        ]);

    [Fact]
    public void Build_ProducesSystemsRoutesAndAiPlayers()
    {
        var data = MapBuilder.Build(MakeMap(), new Random(1), Naming);

        Assert.Equal(3, data.Systems.Count);
        Assert.Equal(SystemOwner.Player, data.Systems[0].Owner);
        Assert.Equal(SystemOwner.Ai1, data.Systems[1].Owner);
        Assert.Equal(5f, data.Systems[0].InitialFleet);
        Assert.Equal("Vrehl", data.Systems[0].Name);
        Assert.Equal("The bridgehead.", data.Systems[0].Description);

        Assert.Equal(3, data.Routes.Count);
        Assert.Equal(AiDisposition.Cautious, Assert.Single(data.AiPlayers).Disposition);
    }

    [Fact]
    public void Build_RoutesNormalizeAndDedupe()
    {
        var map = MakeMap() with
        {
            Routes =
            [
                new MapRoute(1, 0),
                new MapRoute(0, 1)
            ]
        };

        var data = MapBuilder.Build(map, new Random(1), Naming);

        var edge = Assert.Single(data.Routes);
        Assert.Equal((0, 1), (edge.From, edge.To));
    }

    [Fact]
    public void Build_ProducesConnectedGraph()
    {
        var data = MapBuilder.Build(MakeMap(), new Random(1), Naming);
        var routes = data.Routes.Select(r => ((int, int))(r.From, r.To)).ToList();
        var hops = GraphUtils.BfsHopDistances(0, data.Systems.Count, routes);
        Assert.All(hops, d => Assert.True(d >= 0));
    }

    [Fact]
    public void Build_ThrowsWhenNoPlayerSystem()
    {
        var map = MakeMap() with
        {
            Systems = MakeMap().Systems.Select(s => s with { Owner = SystemOwner.None }).ToArray()
        };

        Assert.Throws<ArgumentException>(() => MapBuilder.Build(map, new Random(1), Naming));
    }

    [Fact]
    public void Build_ThrowsOnOutOfRangeRoute()
    {
        var map = MakeMap() with { Routes = [new MapRoute(0, 99)] };

        Assert.Throws<ArgumentException>(() => MapBuilder.Build(map, new Random(1), Naming));
    }

    [Fact]
    public void Build_DefaultsMissingAiDispositionToRandom()
    {
        var map = MakeMap() with
        {
            Systems = MakeMap().Systems.Select(s => s with { Disposition = null }).ToArray()
        };

        var data = MapBuilder.Build(map, new Random(2), Naming);

        var ai = Assert.Single(data.AiPlayers);
        Assert.True(Enum.IsDefined(ai.Disposition));
    }

    [Fact]
    public void TutorialMap_LoadsFromDisk_AndBuilds()
    {
        var definition = ConfigLoader.Load<MapDefinition>("res://config/maps/tutorial.json");

        var data = MapBuilder.Build(definition, new Random(3), Naming);

        Assert.Equal(4, data.Systems.Count);
        Assert.Equal(SystemOwner.Player, data.Systems[0].Owner);
        Assert.Equal("Vrehl", data.Systems[0].Name);
        Assert.Single(data.AiPlayers);
        Assert.Contains(data.Routes, r => r.From == 0 && r.To == 1);

        var routes = data.Routes.Select(r => ((int, int))(r.From, r.To)).ToList();
        var hops = GraphUtils.BfsHopDistances(0, data.Systems.Count, routes);
        Assert.All(hops, d => Assert.True(d >= 0));
    }
}