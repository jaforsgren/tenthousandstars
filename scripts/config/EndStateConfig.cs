namespace Tts.Config;

public record EndCondition(
    int? EnemiesLeft,
    int? SystemsLeft,
    int? TargetSystemHops,
    bool? EliminateTargetPlayer,
    bool? DefendObjectiveSystem,
    float? TimeoutSeconds,
    string? TimeoutMessage,
    string Description,
    string EndDescription);

public record EndStateConfig(float MissionBriefSeconds, float EndStateSeconds, string[] DefeatDescriptions, EndCondition[] Conditions);
