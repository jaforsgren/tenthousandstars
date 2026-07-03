using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly string[] _sectorNames;
    private readonly NarrativeTextConfig _textConfig;
    private readonly Random _rng;

    private NarrativeController(INarrativeService service, INarrativeBarkSystem barkSystem, string[] sectorNames, NarrativeTextConfig textConfig, Random rng)
    {
        _service = service;
        _barkSystem = barkSystem;
        _sectorNames = sectorNames;
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

        // Enrich chapters with bark pools from Yarn
        var chapterBarkPools = YarnLinePool.Load("res://yarn/chapter_barks.yarn");
        chapters = chapters.Select(c => c with
        {
            IntroBarks = YarnLinePool.GetPool(chapterBarkPools, $"{c.Id}_intro"),
            OutroBarks = YarnLinePool.GetPool(chapterBarkPools, $"{c.Id}_outro")
        }).ToList();

        var conditionSet = ConfigLoader.Load<NarrativeConditionSet>("res://config/story/conditions.json");

        // Enrich conditions with text from Yarn
        var endStatePools = YarnLinePool.Load("res://yarn/end_states.yarn");
        var enrichedConditions = conditionSet.Conditions.Select(c => c with
        {
            Description    = YarnLinePool.GetFirst(endStatePools, $"{c.Id}_brief")   ?? c.Description,
            EndDescription = YarnLinePool.GetFirst(endStatePools, $"{c.Id}_end")     ?? c.EndDescription,
            TimeoutMessage = YarnLinePool.GetFirst(endStatePools, $"{c.Id}_timeout") ?? c.TimeoutMessage
        }).ToArray();

        var briefingPools = YarnLinePool.Load("res://yarn/briefings.yarn");

        // Narrative barks — pre-formatted as "[NPC]: Message"
        var rawBarkPools = YarnLinePool.Load("res://yarn/barks.yarn");
        var narrativeBarkPools = rawBarkPools.ToDictionary(
            kv => kv.Key,
            kv => kv.Value
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => {
                    var sep = l.IndexOf(": ", StringComparison.Ordinal);
                    return sep > 0 ? $"[{l[..sep]}]: {l[(sep + 2)..]}" : l;
                })
                .ToArray(),
            StringComparer.Ordinal);

        var textConfig = ConfigLoader.Load<NarrativeTextConfig>("res://config/story/narrative.json");
        var narrativeTextPools = YarnLinePool.Load("res://yarn/narrative_text.yarn");
        var sectorNames = YarnLinePool.GetPool(narrativeTextPools, "sector_names");

        var aiNamingConfig = ConfigLoader.Load<AiNamingConfig>("res://config/ai_naming.json");

        // Enrich scenarios with text from Yarn
        var scenarioConfig = ConfigLoader.Load<ScenarioConfig>("res://config/scenarios.json");

        var scenarioPoolFiles = new[] { "the_relay_chain", "survivor_enclave", "the_ghost_fleet" };
        var scenarioPools = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var file in scenarioPoolFiles)
        {
            var filePools = YarnLinePool.Load($"res://yarn/scenarios/{file}.yarn");
            foreach (var kv in filePools) scenarioPools[kv.Key] = kv.Value;
        }
        var enrichedScenarios = scenarioConfig.Scenarios.Select(s => s with
        {
            Title     = YarnLinePool.GetFirst(scenarioPools, $"{s.Id}_title")      ?? s.Id,
            IntroText = YarnLinePool.GetFirst(scenarioPools, $"{s.Id}_intro_text") ?? "",
            Stages    = s.Stages.Select(stage => stage with
            {
                Text = YarnLinePool.GetText(scenarioPools, $"{s.Id}_{stage.DependsOn ?? "root"}")
            }).ToArray()
        }).ToArray();
        var enrichedScenarioConfig = scenarioConfig with { Scenarios = enrichedScenarios };

        var db = new NarrativeDatabase(archetypes, chapters, enrichedConditions, briefingPools, narrativeBarkPools, enrichedScenarioConfig);
        var service = new NarrativeService(db, new ChapterGenerator(), new MissionGenerator(db), new BriefingGenerator(db, rng), aiNamingConfig, rng);
        var barkSystem = new NarrativeBarkSystem(narrativeBarkPools, rng);

        return new NarrativeController(service, barkSystem, sectorNames, textConfig, rng);
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
        var sectorName = _sectorNames.Length > 0 ? _sectorNames[_rng.Next(_sectorNames.Length)] : "Unknown Sector";
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
        var sectorName = _sectorNames.Length > 0 ? _sectorNames[_rng.Next(_sectorNames.Length)] : "Unknown Sector";
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
