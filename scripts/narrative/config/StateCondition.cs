namespace Tts.Narrative;

// Null means "don't care". All non-null fields must match simultaneously.
public record StateCondition(
    bool? EnemyIsWinning,
    bool? PlayerStrongerThanEnemy);
