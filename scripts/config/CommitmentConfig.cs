using System.Collections.Generic;

namespace Tts.Config;

public sealed record CommitmentConfig
{
    public float ArrivalBaseDurationSeconds { get; init; }
    public float EngagementBaseDurationSeconds { get; init; }
    public float ResolutionBaseDurationSeconds { get; init; }
    public float BaseRiskAccumulationRate { get; init; }
    public float RiskPerCohabitantMultiplier { get; init; }
    public float InterruptPenaltyMultiplier { get; init; }
    public float AlterationRiskThreshold { get; init; }
    public float HiddenStateInitRange { get; init; }
    public FleetInfluencesPerShip FleetInfluencesPerShip { get; init; } = new();
    public Dictionary<string, IntentConfig> Intents { get; init; } = new();
    public AiCommitmentConfig Ai { get; init; } = new();
}

public sealed record FleetInfluencesPerShip
{
    public float Aggression { get; init; }
    public float Discipline { get; init; }
    public float Curiosity { get; init; }
    public float Stability { get; init; }
}

public sealed record IntentConfig
{
    public float ArrivalMultiplier { get; init; }
    public float EngagementMultiplier { get; init; }
    public float ResolutionMultiplier { get; init; }
    public float BaseRiskRate { get; init; }
    public float FullControlThreshold { get; init; }
    public float PartialControlThreshold { get; init; }
    public float FleetDamageScale { get; init; }
    public float SystemInstabilityScale { get; init; }
    public float DefenderFleetScale { get; init; }
    public float HostilityDefenseScale { get; init; }
}

public sealed record AiCommitmentConfig
{
    public int MaxActiveCommitmentsPerPlayer { get; init; }
    public float InterruptRiskThreshold { get; init; }
    // Keys: disposition name → (intent name → weight 0–1)
    public Dictionary<string, Dictionary<string, float>> DispositionIntentWeights { get; init; } = new();
}
