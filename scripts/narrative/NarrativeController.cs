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

        
        // here i need to add mode stuff.. 
        // TODO: make a configload.loadFolder meothod
        foreach (var file in archetypeFiles)
            archetypes.Add(ConfigLoader.Load<ArchetypeConfig>($"res://config/story/archetypes/{file}.json"));

        var chapterFiles = new[] { "retreat", "assault", "skirmish", "conquest" };
        var chapters = new List<ChapterDefConfig>(chapterFiles.Length);
        foreach (var file in chapterFiles)
            chapters.Add(ConfigLoader.Load<ChapterDefConfig>($"res://config/story/chapters/{file}.json"));

        var conditionSet = ConfigLoader.Load<NarrativeConditionSet>("res://config/story/missions/conditions.json");
        var briefingConfig = ConfigLoader.Load<BriefingConfig>("res://config/story/briefings/templates.json");
        var barkConfig = ConfigLoader.Load<BarkConfig>("res://config/barks.json");
        var interludeConfig = ConfigLoader.Load<InterludeConfig>("res://config/story/interludes/templates.json");
        var outroConfig = ResolveOutroConfig(
            ConfigLoader.Load<OutroConfigSource>("res://config/story/outro/templates.json"));
        var aiNamingConfig = ConfigLoader.Load<AiNamingConfig>("res://config/ai_naming.json");

        var db = new NarrativeDatabase(archetypes, chapters, conditionSet.Conditions, briefingConfig, barkConfig, interludeConfig, outroConfig);
        var service = new NarrativeService(db, new ChapterGenerator(), new MissionGenerator(db), new BriefingGenerator(db, rng), aiNamingConfig, rng);
        var barkSystem = new NarrativeBarkSystem(db, rng);
        var outroGenerator = new OutroGenerator(outroConfig, db, rng);

        return new NarrativeController(service, barkSystem, outroGenerator);
    }

    private static OutroConfig ResolveOutroConfig(OutroConfigSource source)
    {
        var templates = new OutroTemplate[source.Templates.Length];
        for (var i = 0; i < source.Templates.Length; i++)
        {
            var t = source.Templates[i];
            var text = ConfigLoader.LoadText($"res://{t.TextFile}");
            templates[i] = new OutroTemplate(t.Id, t.Tags, t.Title, text);
        }
        return new OutroConfig(templates);
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

    public (string Title, string Text) GenerateOutro()
    {
        var (title, text) = _outroGenerator.Generate(_service.CurrentState);
        Log($"[Narrative] Outro: {title}");
        return (title, text);
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
