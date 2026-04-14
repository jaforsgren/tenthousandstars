using System;
using System.Collections.Generic;
using Godot;

namespace Tts;

// Plain C# class (not a Node) — held as a persistent field in Level.
// This is the only class in the narrative module with a Godot dependency (ConfigLoader, GD.Print).
public class NarrativeController
{
    private readonly INarrativeService _service;
    private readonly INarrativeBarkSystem _barkSystem;
    private readonly OutroGenerator _outroGenerator;

    private NarrativeController(INarrativeService service, INarrativeBarkSystem barkSystem, OutroGenerator outroGenerator)
    {
        _service = service;
        _barkSystem = barkSystem;
        _outroGenerator = outroGenerator;
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
        var storyTextConfig = ResolveStoryTextConfig(
            ConfigLoader.Load<StoryTextConfigSource>("res://config/story/texts.json"));
        var aiNamingConfig = ConfigLoader.Load<AiNamingConfig>("res://config/ai_naming.json");
        var scenarioConfig = ConfigLoader.Load<ScenarioConfig>("res://config/scenarios.json");

        var db = new NarrativeDatabase(archetypes, chapters, conditionSet.Conditions, briefingConfig, barkConfig, storyTextConfig, scenarioConfig);
        var service = new NarrativeService(db, new ChapterGenerator(), new MissionGenerator(db), new BriefingGenerator(db, rng), aiNamingConfig, rng);
        var barkSystem = new NarrativeBarkSystem(db, rng);
        var outroGenerator = new OutroGenerator(storyTextConfig, db, rng);

        return new NarrativeController(service, barkSystem, outroGenerator);
    }

    private static StoryTextConfig ResolveStoryTextConfig(StoryTextConfigSource source)
    {
        var templates = new StoryTextTemplate[source.Templates.Length];
        for (var i = 0; i < source.Templates.Length; i++)
        {
            var t = source.Templates[i];
            var text = ConfigLoader.LoadText($"res://{t.TextFile}");
            templates[i] = new StoryTextTemplate(t.Id, t.Tags, t.Title, text);
        }
        return new StoryTextConfig(source.BaseYear, source.DateFormat, source.SectorNames, templates);
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
        Log($"[Narrative] Mission {ctx.State.MissionsCompleted + 1} — chapter: {ctx.Chapter.ChapterId}, condition: {ctx.Condition.Id}");
        Log($"[Narrative] Briefing: {ctx.Briefing}");
        return ctx;
    }

    public void OnMissionComplete(bool won, int playerSystems, int enemySystems, int enemyFleets)
    {
        _service.OnMissionComplete(new MissionResult(won, playerSystems, enemySystems, enemyFleets));
        Log($"[Narrative] Mission complete — won: {won}, chapter: {_service.CurrentState.CurrentChapterIndex}/{_service.CurrentState.TotalChapters}");
    }

    public StoryText GenerateOutro()
    {
        var storyText = _outroGenerator.Generate(_service.CurrentState);
        Log($"[Narrative] Outro: {storyText.Title}");
        return storyText;
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
}
