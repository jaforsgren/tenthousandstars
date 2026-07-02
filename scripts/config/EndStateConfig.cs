namespace Tts.Config;

public record EndCondition(
    string? Id = null,
    int? EnemiesLeft = null,
    int? SystemsLeft = null,
    int? TargetSystemHops = null,
    bool? EliminateTargetPlayer = null,
    bool? DefendObjectiveSystem = null,
    float? TimeoutSeconds = null,
    string? TimeoutMessage = null,
    string Description = "",
    string EndDescription = "");

public record EndStateConfig(float MissionBriefSeconds, float EndStateSeconds, string[] DefeatDescriptions, EndCondition[] Conditions);
