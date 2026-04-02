namespace Tts;

public record MissionResult(
    bool Won,
    int PlayerSystemCount,
    int EnemySystemCount,
    int EnemyFleetCount);
