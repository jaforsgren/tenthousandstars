using System;
using System.Collections.Generic;
using Godot;
using Tts.Config;
using Tts.Utils;

namespace Tts.Narrative;

// Plain C# class (not a Node) — held as a persistent field in Level.
// This is the only class in the narrative module with a Godot dependency (ConfigLoader, GD.Print).
public class NarrativeController
{
    private readonly INarrativeService _service;
    private readonly INarrativeBarkSystem _barkSystem;
    private readonly NarrativeTextConfig _textConfig;
    private readonly Random _rng;

    private NarrativeController(INarrativeService service, INarrativeBarkSystem barkSystem, NarrativeTextConfig textConfig, Random rng)
    {
        _service = service;
        _barkSystem = barkSystem;
        _textConfig = textConfig;
        _rng = rng;
    }

    private static void Log(string msg)
    {
        if (Type.GetType("Godot.Engine, Godot") != null)
            GD.Print(msg);
        else
            Console.WriteLine(msg);
    }

    public static NarrativeController Create(Random rng)
    {
        var archetypeFiles = new[] { "falling_empire", "rising_power", "conquest" };
        var archetypes = new List<ArchetypeConfig>(archetypeFiles.Length);
        foreach (var file in archetypeFiles)
            archetypes.Add(ConfigLoader.Load<ArchetypeConfig>($"res://config/story/archetypes/{file}.json"));

        var chapterFiles = new[] { "retreat", "assault", "skirmish", "conquest" };
        var chapters = new List<ChapterDefConfig>(chapterFiles.Length);
        foreach (var file in chapterFiles)
            chapters.Add(ConfigLoader.Load<ChapterDefConfig>($"res://config/story/chapters/{file}.json"));

        var conditionSet = ConfigLoader.Load<NarrativeConditionSet>("res://config/story/conditions.json");
        var briefingConfig = ConfigLoader.Load<BriefingConfig>("res://config/story/briefings.json");
        var barkConfig = ConfigLoader.Load<BarkConfig>("res://config/barks.json");
        var textConfig = ConfigLoader.Load<NarrativeTextConfig>("res://config/story/narrative.json");
        var aiNamingConfig = ConfigLoader.Load<AiNamingConfig>("res://config/ai_naming.json");
        var scenarioConfig = ConfigLoader.Load<ScenarioConfig>("res://config/scenarios.json");

        var db = new NarrativeDatabase(archetypes, chapters, conditionSet.Conditions, briefingConfig, barkConfig, scenarioConfig);
        var service = new NarrativeService(db, new ChapterGenerator(), new MissionGenerator(db), new BriefingGenerator(db, rng), aiNamingConfig, rng);
        var barkSystem = new NarrativeBarkSystem(db, rng);

        return new NarrativeController(service, barkSystem, textConfig, rng);
    }

    public bool IsCampaignComplete => _service.IsCampaignComplete;

    public StoryState CurrentState => _service.CurrentState;

    public void StartCampaign(string archetypeId = "")
    {
        _service.StartCampaign(archetypeId);
        Log($"[Narrative] Campaign started — archetype: {_service.CurrentState.ArchetypeId}, player: {_service.CurrentState.Player.FactionName}");
    }

    public void UpdateEnemy(Character enemy)
        => _service.UpdateEnemy(enemy);

    public MissionContext GetNextMission()
    {
        var ctx = _service.GetNextMission();
        var sectorName = _textConfig.SectorNames[_rng.Next(_textConfig.SectorNames.Length)];
        var year = _textConfig.BaseYear + ctx.State.MissionsCompleted;
        var date = _textConfig.DateFormat
            .Replace("{MissionIndex}", (ctx.State.MissionsCompleted + 1).ToString())
            .Replace("{Year}", year.ToString());

        Log($"[Narrative] Mission {ctx.State.MissionsCompleted + 1} — chapter: {ctx.Chapter.ChapterId}, interlude: {ctx.InterludeNodeName ?? "none"}");
        Log($"[Narrative] Briefing: {ctx.Briefing}");

        return ctx with { SectorName = sectorName, InterludeDate = date };
    }

    public void OnMissionComplete(bool won, int playerSystems, int enemySystems, int enemyFleets)
    {
        _service.OnMissionComplete(new MissionResult(won, playerSystems, enemySystems, enemyFleets));
        Log($"[Narrative] Mission complete — won: {won}, chapter: {_service.CurrentState.CurrentChapterIndex}/{_service.CurrentState.TotalChapters}");
    }

    public string GetOutroNodeName()
    {
        var state = _service.CurrentState;
        var won = state.MissionsWon * 2 >= state.TotalChapters;
        var winTag = won ? "win" : "loss";

        // Prefer archetype-specific node, fall back to generic
        var archetypeNode = $"outro_{state.ArchetypeId}_{winTag}";
        var genericNode = $"outro_generic_{winTag}";

        // Return archetype node if archetype is known, otherwise generic
        return state.ArchetypeId is "falling_empire" or "rising_power" or "conquest"
            ? archetypeNode
            : genericNode;
    }

    public Dictionary<string, string> BuildOutroVars()
    {
        var state = _service.CurrentState;
        var archetypeName = ArchetypeDisplayName(state.ArchetypeId);
        var sectorName = _textConfig.SectorNames[_rng.Next(_textConfig.SectorNames.Length)];
        var year = _textConfig.BaseYear + state.MissionsCompleted;
        var date = _textConfig.DateFormat
            .Replace("{MissionIndex}", state.MissionsCompleted.ToString())
            .Replace("{Year}", year.ToString());

        return new Dictionary<string, string>
        {
            ["$player_faction"] = state.Player.FactionName,
            ["$enemy_faction"]  = state.Enemy.FactionName,
            ["$missions_won"]   = state.MissionsWon.ToString(),
            ["$total_missions"] = state.TotalChapters.ToString(),
            ["$archetype_name"] = archetypeName,
            ["$sector_name"]    = sectorName,
            ["$date"]           = date,
        };
    }

    public ScenarioDefinition[] SelectEligibleScenarios()
        => _service.SelectEligibleScenarios(_service.CurrentState);

    public ScenarioDefinition[] SelectEligibleScenarios(int missionsPlayed, int missionsWon)
    {
        var fakeState = _service.CurrentState with
        {
            MissionsCompleted = missionsPlayed,
            MissionsWon = missionsWon
        };
        return _service.SelectEligibleScenarios(fakeState);
    }

    public string? TryGetBark(BarkTrigger trigger)
        => _barkSystem.TryGetBark(trigger, _service.CurrentState);

    public void PrintDebugState()
    {
        var s = _service.CurrentState;
        Log($"[Narrative Debug] Archetype: {s.ArchetypeId} | Chapter: {s.CurrentChapterIndex}/{s.TotalChapters} ({s.CurrentChapterId})");
        Log($"[Narrative Debug] Missions: {s.MissionsWon}/{s.MissionsCompleted} won | Enemy winning: {s.EnemyIsWinning} | Player stronger: {s.PlayerStrongerThanEnemy}");
        Log($"[Narrative Debug] Factions: {s.Player.FactionName} ({s.Player.Title}) vs {s.Enemy.FactionName} ({s.Enemy.Title})");
    }

    private static string ArchetypeDisplayName(string id) => id switch
    {
        "falling_empire" => "The Falling Empire",
        "rising_power"   => "The Rising Power",
        "conquest"       => "Total Conquest",
        _                => id
    };
}
