using Tts.Config;
namespace Tts.Narrative;

public record NarrativeConditionConfig(
    string Id,
    string[] Tags,
    int? EnemiesLeft = null,
    int? SystemsLeft = null,
    int? TargetSystemHops = null,
    bool? EliminateTargetPlayer = null,
    bool? DefendObjectiveSystem = null,
    float? TimeoutSeconds = null,
    string? TimeoutMessage = null,
    string Description = "",
    string EndDescription = "")
{
    public EndCondition ToEndCondition() => new(
        Id,
        EnemiesLeft,
        SystemsLeft,
        TargetSystemHops,
        EliminateTargetPlayer,
        DefendObjectiveSystem,
        TimeoutSeconds,
        TimeoutMessage,
        Description,
        EndDescription);
}

public record NarrativeConditionSet(NarrativeConditionConfig[] Conditions);
