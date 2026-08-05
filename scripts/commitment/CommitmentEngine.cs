using System;
using System.Collections.Generic;
using Tts.Config;
using Tts.Level;

namespace Tts.Commitment;

// Pure stateless logic — no Godot dependency.
// CommitmentController owns state and calls into here each tick.
public static class CommitmentEngine
{
    public static void Tick(
        CommitmentState commitment,
        ref SystemHiddenState hiddenState,
        IReadOnlyList<CommitmentState> cohabitants,
        CommitmentConfig config,
        Random rng,
        float delta)
    {
        if (!commitment.IsActive) return;

        var intentCfg = config.Intents[commitment.Intent.ToString()];
        var hostileCount = CountHostileCohabitants(commitment, cohabitants);
        var fortifySlowdown = GetFortifySlowdown(commitment, cohabitants);

        AdvancePhase(commitment, intentCfg, config, fortifySlowdown, delta);
        ApplyIntentInteractions(commitment, ref hiddenState, intentCfg, delta, rng);
        AccumulateRisk(commitment, intentCfg, hostileCount, config, delta);
    }

    public static CommitmentOutcome Resolve(
        CommitmentState commitment,
        SystemHiddenState hiddenState,
        CommitmentConfig config,
        Random rng)
    {
        // Risk introduces variance — high risk = less predictable outcome
        var noise = (float)(rng.NextDouble() * commitment.RiskLevel * 0.4 - 0.2);
        var intentCfg = config.Intents[commitment.Intent.ToString()];

        return commitment.Intent switch
        {
            IntentType.Attack      => ResolveAttack(commitment, hiddenState, intentCfg, noise),
            IntentType.Contest     => ResolveContest(commitment, hiddenState, intentCfg, noise),
            IntentType.Fortify     => ResolveFortify(commitment, intentCfg),
            IntentType.Investigate => ResolveInvestigate(commitment, hiddenState, intentCfg, noise),
            IntentType.Exploit     => ResolveExploit(commitment, hiddenState, intentCfg, noise, rng),
            _                      => CommitmentOutcome.Empty
        };
    }

    public static (InterruptPenalty Penalty, CommitmentOutcome PartialOutcome) Interrupt(
        CommitmentState commitment,
        CommitmentConfig config)
    {
        commitment.IsInterrupted = true;

        // Later phases cost more on interrupt — partial progress turns against you
        var phaseWeight = commitment.Phase switch
        {
            CommitmentPhase.Arrival    => 0.1f,
            CommitmentPhase.Engagement => 0.5f + commitment.PhaseProgress * 0.3f,
            CommitmentPhase.Resolution => 0.8f + commitment.PhaseProgress * 0.15f,
            _                          => 0.5f
        };

        var penalty = new InterruptPenalty(
            StrengthLost: commitment.RiskLevel * phaseWeight * config.InterruptPenaltyMultiplier,
            InstabilityAdded: commitment.AccumulatedInstabilityContribution * phaseWeight,
            FleetReturnsAltered: commitment.RiskLevel > config.AlterationRiskThreshold);

        var partialOutcome = commitment.Phase >= CommitmentPhase.Engagement
            ? BuildNegativePartialOutcome(commitment, phaseWeight)
            : CommitmentOutcome.Empty;

        return (penalty, partialOutcome);
    }

    public static SystemVisibleState ComputeVisibleState(
        IReadOnlyList<CommitmentState> commitments,
        SystemHiddenState hiddenState)
    {
        var activeCount = 0;
        var hasAttack = false;
        var ownerCount = 0;
        SystemOwner lastOwner = default;

        foreach (var c in commitments)
        {
            if (!c.IsActive) continue;
            activeCount++;
            if (c.Intent == IntentType.Attack) hasAttack = true;
            if (c.Owner != lastOwner)
            {
                ownerCount++;
                lastOwner = c.Owner;
            }
        }

        var threat = hiddenState.Hostility switch
        {
            > 0.7f => "Volatile",
            > 0.4f => "Tense",
            _      => "Stable"
        };

        var activity = (activeCount, hasAttack, ownerCount > 1) switch
        {
            (0, _, _)      => "Quiet",
            (_, _, true)   => "Conflicted",
            (_, true, _)   => "Contested",
            _              => "Active"
        };

        return new SystemVisibleState(threat, activity);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Phase advancement

    private static void AdvancePhase(
        CommitmentState commitment,
        IntentConfig intentCfg,
        CommitmentConfig config,
        float fortifySlowdown,
        float delta)
    {
        var duration = GetPhaseDuration(commitment.Phase, intentCfg, config);
        var rate = (1f / duration) * delta;

        // Opposing fortify slows engagement/resolution progress
        if (commitment.Phase != CommitmentPhase.Arrival)
            rate *= MathF.Max(0.1f, 1f - fortifySlowdown * 0.35f);

        commitment.PhaseProgress += rate;

        if (commitment.PhaseProgress < 1f) return;

        commitment.PhaseProgress -= 1f;

        if (commitment.Phase == CommitmentPhase.Resolution)
        {
            commitment.IsComplete = true;
            return;
        }

        commitment.Phase = commitment.Phase switch
        {
            CommitmentPhase.Arrival    => CommitmentPhase.Engagement,
            CommitmentPhase.Engagement => CommitmentPhase.Resolution,
            _                          => commitment.Phase
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Interaction with system hidden state (per intent)

    private static void ApplyIntentInteractions(
        CommitmentState commitment,
        ref SystemHiddenState state,
        IntentConfig intentCfg,
        float delta,
        Random rng)
    {
        var noise = (float)(rng.NextDouble() * 0.06 - 0.03); // ±3% variation per tick

        switch (commitment.Intent)
        {
            case IntentType.Attack:
            {
                var pressure = commitment.Influences.Aggression * delta * intentCfg.EngagementMultiplier;
                // Attacker wears down resistance but stirs up hostility
                state.ResistanceLevel = Math.Clamp(state.ResistanceLevel - pressure, 0f, 1f);
                state.Hostility       = Math.Clamp(state.Hostility + pressure * 0.4f + noise, 0f, 1f);
                commitment.AccumulatedFleetDamage             += state.ResistanceLevel * delta * intentCfg.FleetDamageScale;
                commitment.AccumulatedInstabilityContribution += pressure * intentCfg.SystemInstabilityScale;
                break;
            }
            case IntentType.Contest:
            {
                var pressure = commitment.Influences.Discipline * delta * intentCfg.EngagementMultiplier;
                state.Instability = Math.Clamp(state.Instability + pressure + noise, 0f, 1f);
                commitment.AccumulatedInstabilityContribution += pressure;
                break;
            }
            case IntentType.Fortify:
            {
                var pressure = commitment.Influences.Stability * delta * intentCfg.EngagementMultiplier;
                state.Instability = Math.Clamp(state.Instability - pressure, 0f, 1f);
                state.Hostility   = Math.Clamp(state.Hostility - pressure * 0.25f, 0f, 1f);
                break;
            }
            case IntentType.Investigate:
            {
                var pressure = commitment.Influences.Curiosity * delta;
                // Poking at relics or corruption slowly stirs instability
                commitment.AccumulatedInstabilityContribution += pressure * state.RelicPresence * 0.15f;
                break;
            }
            case IntentType.Exploit:
            {
                var pressure = (commitment.Influences.Aggression + commitment.Influences.Discipline)
                               * 0.5f * delta * intentCfg.EngagementMultiplier;
                state.Corruption  = Math.Clamp(state.Corruption + pressure * intentCfg.SystemInstabilityScale, 0f, 1f);
                state.Instability = Math.Clamp(state.Instability + pressure * 0.25f + noise, 0f, 1f);
                break;
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Resolution per intent

    private static CommitmentOutcome ResolveAttack(
        CommitmentState c,
        SystemHiddenState hidden,
        IntentConfig cfg,
        float noise)
    {
        var score = c.Influences.Aggression
                    - (c.DefenderFleetAtCommitment * cfg.DefenderFleetScale + hidden.Hostility * cfg.HostilityDefenseScale)
                    + noise;

        var control = score switch
        {
            _ when score >= cfg.FullControlThreshold    => ControlChange.Full,
            _ when score >= cfg.PartialControlThreshold  => ControlChange.Partial,
            _                                            => ControlChange.None
        };

        return new CommitmentOutcome(
            control,
            new FleetChanges(-c.AccumulatedFleetDamage - hidden.ResistanceLevel * 0.2f, 0f, 0f),
            new SystemChanges(c.AccumulatedInstabilityContribution, 0f, false),
            new SideEffects(hidden.Instability > 0.7f, null, hidden.Instability * 0.3f));
    }

    private static CommitmentOutcome ResolveContest(
        CommitmentState c,
        SystemHiddenState hidden,
        IntentConfig cfg,
        float noise)
    {
        var destabilization = c.AccumulatedInstabilityContribution + noise;
        var control = destabilization >= cfg.PartialControlThreshold ? ControlChange.Partial : ControlChange.None;

        return new CommitmentOutcome(
            control,
            new FleetChanges(-c.AccumulatedFleetDamage * 0.3f, 0f, -0.05f),
            new SystemChanges(destabilization, 0.05f, false),
            new SideEffects(false, null, 0f));
    }

    private static CommitmentOutcome ResolveFortify(CommitmentState c, IntentConfig cfg)
    {
        var stabilization = c.Influences.Stability * cfg.EngagementMultiplier;
        return new CommitmentOutcome(
            ControlChange.None,
            new FleetChanges(0f, 0f, c.Influences.Stability * 0.1f),
            new SystemChanges(-stabilization, -0.05f, false),
            new SideEffects(false, null, 0f));
    }

    private static CommitmentOutcome ResolveInvestigate(
        CommitmentState c,
        SystemHiddenState hidden,
        IntentConfig cfg,
        float noise)
    {
        var score = c.Influences.Curiosity - hidden.Corruption * 0.5f + noise;
        var reveals = score >= cfg.PartialControlThreshold;

        return new CommitmentOutcome(
            ControlChange.None,
            new FleetChanges(0f, 0f, c.Influences.Curiosity * 0.05f),
            new SystemChanges(hidden.RelicPresence * 0.1f, 0f, reveals),
            new SideEffects(hidden.Corruption > 0.6f, null, hidden.Corruption * 0.3f));
    }

    private static CommitmentOutcome ResolveExploit(
        CommitmentState c,
        SystemHiddenState hidden,
        IntentConfig cfg,
        float noise,
        Random rng)
    {
        var extractionScore = (c.Influences.Aggression + c.Influences.Discipline) * 0.5f
                              * cfg.EngagementMultiplier + noise;
        var revolt = hidden.Instability > 0.75f && (float)rng.NextDouble() < hidden.Instability;

        return new CommitmentOutcome(
            ControlChange.None,
            new FleetChanges(extractionScore, 0.1f, 0f),
            new SystemChanges(hidden.Corruption * 0.3f, hidden.Corruption * 0.1f, false),
            new SideEffects(revolt, null, revolt ? hidden.Instability : 0f));
    }

    private static CommitmentOutcome BuildNegativePartialOutcome(CommitmentState c, float phaseWeight)
    {
        // Interrupted mid-engagement: partial progress converts to blowback
        return new CommitmentOutcome(
            ControlChange.None,
            new FleetChanges(-c.AccumulatedFleetDamage * phaseWeight, -0.05f, 0f),
            new SystemChanges(c.AccumulatedInstabilityContribution * phaseWeight * 0.5f, 0f, false),
            new SideEffects(false, null, 0f));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers

    private static void AccumulateRisk(
        CommitmentState commitment,
        IntentConfig intentCfg,
        int hostileCount,
        CommitmentConfig config,
        float delta)
    {
        var rate = intentCfg.BaseRiskRate
                   + config.BaseRiskAccumulationRate
                   + hostileCount * config.RiskPerCohabitantMultiplier;
        commitment.RiskLevel = Math.Clamp(commitment.RiskLevel + rate * delta, 0f, 1f);
    }

    private static float GetPhaseDuration(CommitmentPhase phase, IntentConfig intentCfg, CommitmentConfig config)
        => phase switch
        {
            CommitmentPhase.Arrival    => config.ArrivalBaseDurationSeconds * intentCfg.ArrivalMultiplier,
            CommitmentPhase.Engagement => config.EngagementBaseDurationSeconds * intentCfg.EngagementMultiplier,
            CommitmentPhase.Resolution => config.ResolutionBaseDurationSeconds * intentCfg.ResolutionMultiplier,
            _                          => config.EngagementBaseDurationSeconds
        };

    private static int CountHostileCohabitants(CommitmentState commitment, IReadOnlyList<CommitmentState> cohabitants)
    {
        var count = 0;
        foreach (var other in cohabitants)
        {
            if (other.Id == commitment.Id) continue;
            if (!other.IsActive) continue;
            if (other.Owner != commitment.Owner) count++;
        }
        return count;
    }

    // Returns the stability influence of any opposing Fortify commitment in the same system
    private static float GetFortifySlowdown(CommitmentState commitment, IReadOnlyList<CommitmentState> cohabitants)
    {
        if (commitment.Intent == IntentType.Fortify) return 0f;
        foreach (var other in cohabitants)
        {
            if (!other.IsActive) continue;
            if (other.Intent == IntentType.Fortify && other.Owner != commitment.Owner)
                return other.Influences.Stability;
        }
        return 0f;
    }
}
