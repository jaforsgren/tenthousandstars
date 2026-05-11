using Tts.Config;
namespace Tts.Narrative;

public record NarrativeConditionConfig(
    string Id,
    string[] Tags,
    int? EnemiesLeft,
    int? SystemsLeft,
    int? TargetSystemHops,
    bool? EliminateTargetPlayer,
    bool? DefendObjectiveSystem,
    float? TimeoutSeconds,
    string? TimeoutMessage,
    string Description,
    string EndDescription)
{
    public EndCondition ToEndCondition() => new(
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
