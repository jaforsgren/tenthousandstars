namespace Tts.Narrative;

public record StoryState(
    int MissionsCompleted,
    int MissionsWon,
    int TotalChapters,
    int CurrentChapterIndex,
    string CurrentChapterId,
    string ArchetypeId,
    Character Player,
    Character Enemy,
    bool LastMissionWon,
    bool EnemyIsWinning,
    bool PlayerStrongerThanEnemy);
