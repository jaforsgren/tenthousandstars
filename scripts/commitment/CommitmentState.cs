using System;
using Tts.Level;

namespace Tts.Commitment;

public sealed class CommitmentState
{
    public Guid Id { get; } = Guid.NewGuid();
    public int SystemIndex { get; init; }
    public SystemOwner Owner { get; init; }
    public IntentType Intent { get; init; }
    public FleetInfluences Influences { get; internal set; }
    public float DefenderFleetAtCommitment { get; init; }
    public double StartTime { get; init; }

    public float InitialFleetStrength { get; internal set; }

    public CommitmentPhase Phase { get; internal set; } = CommitmentPhase.Arrival;
    public float PhaseProgress { get; internal set; }
    public float RiskLevel { get; internal set; }

    // Accumulated during engagement — used by Resolve and Interrupt
    public float AccumulatedFleetDamage { get; internal set; }
    public float AccumulatedInstabilityContribution { get; internal set; }

    public bool IsComplete { get; internal set; }
    public bool IsInterrupted { get; internal set; }
    public bool IsActive => !IsComplete && !IsInterrupted;
}
