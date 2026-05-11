using System;
using System.Linq;

namespace Tts.Narrative;

public class NarrativeService : INarrativeService
{
    private readonly NarrativeDatabase _db;
    private readonly IChapterGenerator _chapterGenerator;
    private readonly IMissionGenerator _missionGenerator;
    private readonly IBriefingGenerator _briefingGenerator;
    private readonly AiNamingConfig _aiNamingCfg;
    private readonly Random _rng;

    private ArchetypeConfig _archetype = null!;
    private IInterludeGenerator _interludeGenerator = null!;
    private int _missionsCompleted;
    private int _missionsWon;
    private bool _lastMissionWon;
    private int _currentChapterIndex;
    private Character _player = null!;
    private Character _enemy = null!;

    public StoryState CurrentState { get; private set; } = null!;
    public bool IsCampaignComplete { get; private set; }

    public NarrativeService(
        NarrativeDatabase db,
        IChapterGenerator chapterGenerator,
        IMissionGenerator missionGenerator,
        IBriefingGenerator briefingGenerator,
        AiNamingConfig aiNamingCfg,
        Random rng)
    {
        _db = db;
        _chapterGenerator = chapterGenerator;
        _missionGenerator = missionGenerator;
        _briefingGenerator = briefingGenerator;
        _aiNamingCfg = aiNamingCfg;
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
        _player = AiNaming.GenerateCharacter(_aiNamingCfg, _archetype.PlayerDisposition, _rng);
        _enemy = AiNaming.GenerateCharacter(_aiNamingCfg, (AiDisposition)_rng.Next(Enum.GetValues<AiDisposition>().Length), _rng);

        _interludeGenerator = new InterludeGenerator(_db.StoryTexts, _rng, _archetype.Id);
        CurrentState = BuildState();
    }

    public void UpdateEnemy(Character enemy)
    {
        _enemy = enemy;
        CurrentState = CurrentState with { Enemy = enemy };
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
        var contextWithoutInterlude = new MissionContext(condition, briefing, chapter, CurrentState, Interlude: null, Scenarios: []);
        var interlude = _interludeGenerator.TryGenerate(contextWithoutInterlude);
        var scenarios = SelectEligibleScenarios(CurrentState);

        return contextWithoutInterlude with { Interlude = interlude, Scenarios = scenarios };
    }

    public ScenarioDefinition[] SelectEligibleScenarios(StoryState state)
    {
        var played = state.MissionsCompleted;
        var won = state.MissionsWon;
        var lost = played - won;
        return _db.Scenarios.Scenarios
            .Where(s => IsCriteriaMet(s.Criteria, played, won, lost))
            .ToArray();
    }

    private static bool IsCriteriaMet(ScenarioCriteria c, int played, int won, int lost)
    {
        if (played < c.MinMissionsPlayed) return false;
        if (c.MinMissionsWon.HasValue && won < c.MinMissionsWon.Value) return false;
        if (c.MinMissionsLost.HasValue && lost < c.MinMissionsLost.Value) return false;
        return true;
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
            Player: _player,
            Enemy: _enemy,
            LastMissionWon: _lastMissionWon,
            EnemyIsWinning: enemyIsWinning,
            PlayerStrongerThanEnemy: playerStronger);
    }
}
