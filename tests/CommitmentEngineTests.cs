using System;
using Tts.Commitment;
using Tts.Config;
using Tts.Level;
using Xunit;

namespace Tts.Tests;

public class CommitmentEngineTests
{
	private static CommitmentConfig MakeConfig() => new()
	{
		ArrivalBaseDurationSeconds    = 10f,
		EngagementBaseDurationSeconds = 10f,
		ResolutionBaseDurationSeconds = 10f,
		BaseRiskAccumulationRate      = 0.01f,
		RiskPerCohabitantMultiplier   = 0.02f,
		InterruptPenaltyMultiplier    = 1f,
		AlterationRiskThreshold       = 0.5f,
		HiddenStateInitRange          = 0.5f,
		Intents = new System.Collections.Generic.Dictionary<string, IntentConfig>
		{
			["Attack"] = new()
			{
				ArrivalMultiplier      = 1f,
				EngagementMultiplier   = 1f,
				ResolutionMultiplier   = 1f,
				BaseRiskRate           = 0.01f,
				FullControlThreshold   = 3f,
				PartialControlThreshold = 0f,
				FleetDamageScale       = 0.01f,
				SystemInstabilityScale = 0.01f,
				DefenderFleetScale     = 1f,
				HostilityDefenseScale  = 1f,
			},
		}
	};

	private static CommitmentState MakeState(IntentType intent, float aggression = 5f)
		=> new()
		{
			SystemIndex = 0,
			Owner = SystemOwner.Player,
			Intent = intent,
			Influences = new FleetInfluences(aggression, 5f, 5f, 5f),
			DefenderFleetAtCommitment = 0f,
			StartTime = 0f,
		};

	[Fact]
	public void Tick_AdvancesPhaseAndCompletesAfterFullCycles()
	{
		var state = MakeState(IntentType.Attack);
		var hidden = new SystemHiddenState(0f, 0f, 0f, 0f, 0f);

		for (var i = 0; i < 300; i++)
			CommitmentEngine.Tick(state, ref hidden, [], MakeConfig(), new Random(1), 1f);

		Assert.True(state.IsComplete);
		Assert.False(state.IsInterrupted);
	}

	[Fact]
	public void Tick_DoesNotAdvanceCompleteCommitment()
	{
		var state = MakeState(IntentType.Attack);
		state.IsComplete = true;
		var hidden = new SystemHiddenState(0f, 0f, 0f, 0f, 0f);

		CommitmentEngine.Tick(state, ref hidden, [], MakeConfig(), new Random(1), 1f);

		Assert.Equal(0f, state.PhaseProgress);
	}

	[Fact]
	public void Resolve_StrongAggressionYieldsFullControl()
	{
		var state = MakeState(IntentType.Attack, aggression: 10f);
		var outcome = CommitmentEngine.Resolve(state, new SystemHiddenState(0f, 0f, 0f, 0f, 0f), MakeConfig(), new Random(1));
		Assert.Equal(ControlChange.Full, outcome.ControlChange);
	}

	[Fact]
	public void Resolve_WeakAggressionYieldsNoControl()
	{
		var state = MakeState(IntentType.Attack, aggression: 1f);
		var outcome = CommitmentEngine.Resolve(state, new SystemHiddenState(0.9f, 0.9f, 0f, 0f, 0f), MakeConfig(), new Random(1));
		Assert.Equal(ControlChange.None, outcome.ControlChange);
	}

	[Fact]
	public void Interrupt_MarksInterruptedAndAppliesPenalty()
	{
		var state = MakeState(IntentType.Attack);
		state.RiskLevel = 0.6f;
		state.Phase = CommitmentPhase.Engagement;
		state.PhaseProgress = 0.5f;

		var (penalty, _) = CommitmentEngine.Interrupt(state, MakeConfig());

		Assert.True(state.IsInterrupted);
		Assert.True(penalty.StrengthLost > 0f);
		Assert.True(penalty.FleetReturnsAltered);
	}

	[Fact]
	public void ComputeVisibleState_QuietWhenNoActiveCommitments()
	{
		var visible = CommitmentEngine.ComputeVisibleState([], new SystemHiddenState(0f, 0f, 0f, 0f, 0f));
		Assert.Equal("Quiet", visible.ActivitySignal);
		Assert.Equal("Stable", visible.ThreatSignal);
	}

	[Fact]
	public void ComputeVisibleState_ContestedWhenAttackPresent()
	{
		var state = MakeState(IntentType.Attack);
		var visible = CommitmentEngine.ComputeVisibleState([state], new SystemHiddenState(0f, 0.9f, 0f, 0f, 0f));
		Assert.Equal("Contested", visible.ActivitySignal);
		Assert.Equal("Volatile", visible.ThreatSignal);
	}
}
