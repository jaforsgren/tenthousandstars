using System;

namespace Tts;

public class NarrativeService : INarrativeService
{
    private readonly NarrativeDatabase _db;
    private readonly IChapterGenerator _chapterGenerator;
    private readonly IMissionGenerator _missionGenerator;
    private readonly IBriefingGenerator _briefingGenerator;
    private readonly Random _rng;

    private ArchetypeConfig _archetype = null!;
    private IInterludeGenerator _interludeGenerator = null!;
    private int _missionsCompleted;
    private int _missionsWon;
    private bool _lastMissionWon;
    private int _currentChapterIndex;
    private string _enemyFactionName = "The Enemy";

    public StoryState CurrentState { get; private set; } = null!;
    public bool IsCampaignComplete { get; private set; }

    public NarrativeService(
        NarrativeDatabase db,
        IChapterGenerator chapterGenerator,
        IMissionGenerator missionGenerator,
        IBriefingGenerator briefingGenerator,
        Random rng)
    {
        _db = db;
        _chapterGenerator = chapterGenerator;
        _missionGenerator = missionGenerator;
        _briefingGenerator = briefingGenerator;
        _rng = rng;
    }

    public void StartCampaign(string archetypeId)
    {
        _archetype = string.IsNullOrEmpty(archetypeId)
            ? _db.Archetypes[_rng.Next(_db.Archetypes.Count)]
            : _db.GetArchetype(archetypeId);

        _missionsCompleted = 0;
        _missionsWon = 0;
        _lastMissionWon = false;
        _currentChapterIndex = 0;
        IsCampaignComplete = false;

        _interludeGenerator = new InterludeGenerator(_db.Interludes, _rng, _archetype.Id);
        CurrentState = BuildState();
    }

    public void UpdateEnemyFactionName(string enemyFaction)
    {
        _enemyFactionName = enemyFaction;
        CurrentState = CurrentState with { EnemyFactionName = enemyFaction };
    }

    public MissionContext GetNextMission()
    {
        if (IsCampaignComplete)
            throw new InvalidOperationException("Campaign is already complete.");

        var chapterId = _archetype.ChapterSequence[_currentChapterIndex];
        var chapterDef = _db.GetChapter(chapterId);
        var chapter = _chapterGenerator.GenerateChapter(chapterDef, CurrentState);
        var condition = _missionGenerator.SelectCondition(chapterDef.MissionTags, CurrentState, _rng);
        var briefing = _briefingGenerator.GenerateBriefing(condition, chapter, CurrentState);

        // Interlude requires a partially-built MissionContext for token substitution, so build without interlude first
        var contextWithoutInterlude = new MissionContext(condition, briefing, chapter, CurrentState, Interlude: null);
        var interlude = _interludeGenerator.TryGenerate(contextWithoutInterlude);

        return contextWithoutInterlude with { Interlude = interlude };
    }

    public void OnMissionComplete(MissionResult result)
    {
        _missionsCompleted++;
        if (result.Won) _missionsWon++;
        _lastMissionWon = result.Won;

        _currentChapterIndex++;
        IsCampaignComplete = _currentChapterIndex >= _archetype.ChapterSequence.Length;

        CurrentState = BuildState(
            enemyIsWinning: result.EnemySystemCount > result.PlayerSystemCount,
            playerStronger: result.PlayerSystemCount > result.EnemySystemCount);
    }

    private StoryState BuildState(bool enemyIsWinning = false, bool playerStronger = true)
    {
        var chapterId = _currentChapterIndex < _archetype.ChapterSequence.Length
            ? _archetype.ChapterSequence[_currentChapterIndex]
            : _archetype.ChapterSequence[^1];

        return new StoryState(
            MissionsCompleted: _missionsCompleted,
            MissionsWon: _missionsWon,
            TotalChapters: _archetype.ChapterSequence.Length,
            CurrentChapterIndex: _currentChapterIndex,
            CurrentChapterId: chapterId,
            ArchetypeId: _archetype.Id,
            PlayerFactionName: _archetype.PlayerFactionName,
            EnemyFactionName: _enemyFactionName,
            LastMissionWon: _lastMissionWon,
            EnemyIsWinning: enemyIsWinning,
            PlayerStrongerThanEnemy: playerStronger);
    }
}
