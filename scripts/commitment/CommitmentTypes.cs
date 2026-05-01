namespace Tts;

public enum IntentType { Attack, Contest, Fortify, Investigate, Exploit }
public enum CommitmentPhase { Arrival, Engagement, Resolution }
public enum ControlChange { None, Partial, Full }

public readonly record struct FleetInfluences(
    float Aggression,
    float Discipline,
    float Curiosity,
    float Stability);

// Mutable — updated each tick by CommitmentEngine interactions
public record struct SystemHiddenState(
    float Instability,
    float Hostility,
    float RelicPresence,
    float Corruption,
    float ResistanceLevel);

// Vague signals shown to players — derived from hidden state, never exposes raw values
public readonly record struct SystemVisibleState(
    string ThreatSignal,
    string ActivitySignal);

public readonly record struct FleetChanges(
    float StrengthDelta,
    float AggressionDelta,
    float DisciplineDelta);

public readonly record struct SystemChanges(
    float InstabilityDelta,
    float CorruptionDelta,
    bool RevealHiddenAspect);

public readonly record struct SideEffects(
    bool SpreadToNeighbor,
    int? NeighborSystemIndex,
    float DelayedTriggerStrength);

public readonly record struct CommitmentOutcome(
    ControlChange ControlChange,
    FleetChanges FleetChanges,
    SystemChanges SystemChanges,
    SideEffects SideEffects)
{
    public static CommitmentOutcome Empty => new(
        ControlChange.None,
        new FleetChanges(0f, 0f, 0f),
        new SystemChanges(0f, 0f, false),
        new SideEffects(false, null, 0f));
}

public readonly record struct InterruptPenalty(
    float StrengthLost,
    float InstabilityAdded,
    bool FleetReturnsAltered);

public readonly record struct AiCommitmentDecision(int SystemIndex, IntentType Intent);
