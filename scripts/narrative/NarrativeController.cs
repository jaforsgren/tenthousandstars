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

    private NarrativeController(INarrativeService service, INarrativeBarkSystem barkSystem)
    {
        _service = service;
        _barkSystem = barkSystem;
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

        var conditionSet = ConfigLoader.Load<NarrativeConditionSet>("res://config/story/missions/conditions.json");
        var briefingConfig = ConfigLoader.Load<BriefingConfig>("res://config/story/briefings/templates.json");
        var barkConfig = ConfigLoader.Load<NarrativeBarkConfig>("res://config/story/barks/barks.json");
        var interludeConfig = ConfigLoader.Load<InterludeConfig>("res://config/story/interludes/templates.json");

        var db = new NarrativeDatabase(archetypes, chapters, conditionSet.Conditions, briefingConfig, barkConfig, interludeConfig);
        var service = new NarrativeService(db, new ChapterGenerator(), new MissionGenerator(db), new BriefingGenerator(db, rng), rng);
        var barkSystem = new NarrativeBarkSystem(db, rng);

        return new NarrativeController(service, barkSystem);
    }

    public bool IsCampaignComplete => _service.IsCampaignComplete;

    public StoryState CurrentState => _service.CurrentState;

    public void StartCampaign(string archetypeId = "")
    {
        _service.StartCampaign(archetypeId);
        GD.Print($"[Narrative] Campaign started — archetype: {_service.CurrentState.ArchetypeId}, player: {_service.CurrentState.PlayerFactionName}");
    }

    public void UpdateEnemyFaction(string enemyFaction)
        => _service.UpdateEnemyFactionName(enemyFaction);

    public MissionContext GetNextMission()
    {
        var ctx = _service.GetNextMission();
        GD.Print($"[Narrative] Mission {ctx.State.MissionsCompleted + 1} — chapter: {ctx.Chapter.ChapterId}, condition: {ctx.Condition.Id}");
        GD.Print($"[Narrative] Briefing: {ctx.Briefing}");
        return ctx;
    }

    public void OnMissionComplete(bool won, int playerSystems, int enemySystems, int enemyFleets)
    {
        _service.OnMissionComplete(new MissionResult(won, playerSystems, enemySystems, enemyFleets));
        GD.Print($"[Narrative] Mission complete — won: {won}, chapter: {_service.CurrentState.CurrentChapterIndex}/{_service.CurrentState.TotalChapters}");
    }

    public string? TryGetBark(BarkTrigger trigger)
        => _barkSystem.TryGetBark(trigger, _service.CurrentState);

    public void PrintDebugState()
    {
        var s = _service.CurrentState;
        GD.Print($"[Narrative Debug] Archetype: {s.ArchetypeId} | Chapter: {s.CurrentChapterIndex}/{s.TotalChapters} ({s.CurrentChapterId})");
        GD.Print($"[Narrative Debug] Missions: {s.MissionsWon}/{s.MissionsCompleted} won | Enemy winning: {s.EnemyIsWinning} | Player stronger: {s.PlayerStrongerThanEnemy}");
        GD.Print($"[Narrative Debug] Factions: {s.PlayerFactionName} vs {s.EnemyFactionName}");
    }
}
