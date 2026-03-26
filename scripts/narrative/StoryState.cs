namespace Tts;

public record StoryState(
    int MissionsCompleted,
    int MissionsWon,
    int TotalChapters,
    int CurrentChapterIndex,
    string CurrentChapterId,
    string ArchetypeId,
    string PlayerFactionName,
    string EnemyFactionName,
    bool LastMissionWon,
    bool EnemyIsWinning,
    bool PlayerStrongerThanEnemy);
