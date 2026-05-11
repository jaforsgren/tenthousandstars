using System;
using System.Collections.Generic;

namespace Tts.Commitment;

// Pure stateless AI decision logic for the commitment system.
// AI infers hidden state from visible signals — it never reads SystemHiddenState directly.
public static class AiCommitmentEvaluator
{
    public static AiCommitmentDecision? Evaluate(
        SystemOwner owner,
        AiDisposition disposition,
        IReadOnlyList<SystemNode> systems,
        IReadOnlyList<CommitmentState> activeCommitments,
        Func<int, SystemVisibleState> getVisible,
        CommitmentConfig config,
        Random rng)
    {
        if (disposition == AiDisposition.Dormant) return null;

        var aiCfg = config.Ai;
        if (CountOwnedActiveCommitments(owner, activeCommitments) >= aiCfg.MaxActiveCommitmentsPerPlayer)
            return null;

        if (!aiCfg.DispositionIntentWeights.TryGetValue(disposition.ToString(), out var weights))
            return null;

        var bestScore = 0.1f; // minimum threshold — don't commit if nothing is worth it
        AiCommitmentDecision? best = null;

        for (var i = 0; i < systems.Count; i++)
        {
            var visible = getVisible(i);
            var estimatedRisk = EstimateRiskFromSignals(visible);

            foreach (IntentType intent in Enum.GetValues<IntentType>())
            {
                if (!weights.TryGetValue(intent.ToString(), out var weight) || weight <= 0f) continue;
                if (!IsIntentApplicable(intent, owner, systems[i])) continue;

                var score = ScoreIntent(i, intent, owner, systems[i], visible, estimatedRisk) * weight;
                score += (float)rng.NextDouble() * 0.05f; // small noise prevents always picking the same target

                if (score <= bestScore) continue;
                bestScore = score;
                best = new AiCommitmentDecision(i, intent);
            }
        }

        return best;
    }

    // Decide whether an in-progress commitment should be abandoned.
    // AI compares current risk against a threshold, with intent-specific tolerance.
    public static bool ShouldInterrupt(
        CommitmentState commitment,
        SystemVisibleState visible,
        CommitmentConfig config)
    {
        // Never abandon resolution — too late to back out
        if (commitment.Phase == CommitmentPhase.Resolution) return false;

        var threshold = config.Ai.InterruptRiskThreshold;

        // Attack commitments get extra tolerance in engagement — backing out is costly
        if (commitment.Intent == IntentType.Attack && commitment.Phase == CommitmentPhase.Engagement)
            threshold += 0.15f;

        return commitment.RiskLevel >= threshold;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scoring

    private static float ScoreIntent(
        int systemIndex,
        IntentType intent,
        SystemOwner owner,
        SystemNode system,
        SystemVisibleState visible,
        float estimatedRisk)
    {
        return intent switch
        {
            IntentType.Attack =>
                // More enemy ships = higher threat removed; volatile systems are harder to crack
                (system.Ships * 0.08f) - estimatedRisk * 0.6f,

            IntentType.Contest =>
                // Destabilising is low risk but only useful against controlled systems
                0.35f - estimatedRisk * 0.3f,

            IntentType.Fortify =>
                // Defending owned systems adjacent to conflict matters more
                visible.ActivitySignal is "Contested" or "Conflicted" ? 0.5f : 0.2f,

            IntentType.Investigate =>
                // Only interesting if there's something to learn
                visible.ActivitySignal != "Quiet" ? 0.25f - estimatedRisk * 0.2f : 0f,

            IntentType.Exploit =>
                // Production rate drives exploit value; high instability deters it
                system.ProductionRate * 0.15f - estimatedRisk * 0.4f,

            _ => 0f
        };
    }

    private static bool IsIntentApplicable(IntentType intent, SystemOwner owner, SystemNode system)
        => intent switch
        {
            IntentType.Attack      => system.OwnerPlayer != owner,
            IntentType.Contest     => system.OwnerPlayer != owner,
            IntentType.Fortify     => system.OwnerPlayer == owner,
            IntentType.Investigate => true,
            IntentType.Exploit     => system.OwnerPlayer == owner,
            _                      => false
        };

    // Infer risk from the vague visible signals — AI has no access to hidden state values
    private static float EstimateRiskFromSignals(SystemVisibleState visible)
    {
        var threat = visible.ThreatSignal switch
        {
            "Volatile" => 0.75f,
            "Tense"    => 0.4f,
            _          => 0.1f
        };

        var activity = visible.ActivitySignal switch
        {
            "Conflicted" => 0.5f,
            "Contested"  => 0.3f,
            "Active"     => 0.15f,
            _            => 0f
        };

        return Math.Min(1f, threat + activity);
    }

    private static int CountOwnedActiveCommitments(SystemOwner owner, IReadOnlyList<CommitmentState> commitments)
    {
        var count = 0;
        foreach (var c in commitments)
            if (c.Owner == owner && !c.IsComplete && !c.IsInterrupted) count++;
        return count;
    }
}
