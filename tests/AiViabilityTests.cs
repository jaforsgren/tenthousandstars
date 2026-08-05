using System;
using Tts.Ai;
using Xunit;

namespace Tts.Tests;

public class AiViabilityTests
{
	[Fact]
	public void IsViableAttack_Aggressive_WinsOnly()
	{
		Assert.True(AiViability.IsViableAttack(100f, 50f, 1f, AiDisposition.Aggressive, 0f, 0f, new Random(1)));
		Assert.False(AiViability.IsViableAttack(50f, 100f, 1f, AiDisposition.Aggressive, 0f, 0f, new Random(1)));
	}

	[Fact]
	public void IsViableAttack_Strategic_RequiresSpareShips()
	{
		// Attacker wins with remainder 50 (100 - 50)
		Assert.True(AiViability.IsViableAttack(100f, 50f, 1f, AiDisposition.Strategic, 40f, 0f, new Random(1)));
		// Remainder 50 < 60 spare requirement → not viable
		Assert.False(AiViability.IsViableAttack(100f, 50f, 1f, AiDisposition.Strategic, 60f, 0f, new Random(1)));
	}

	[Fact]
	public void IsViableAttack_Cautious_RollsProbability()
	{
		// cautiousAttackChance 1.0 → always viable when winning
		Assert.True(AiViability.IsViableAttack(100f, 50f, 1f, AiDisposition.Cautious, 0f, 1.0f, new Random(1)));
		// chance 0.0 → never viable
		Assert.False(AiViability.IsViableAttack(100f, 50f, 1f, AiDisposition.Cautious, 0f, 0.0f, new Random(1)));
	}

	[Fact]
	public void IsViableAttack_UnknownDisposition_False()
	{
		Assert.False(AiViability.IsViableAttack(100f, 50f, 1f, AiDisposition.Dormant, 0f, 0f, new Random(1)));
	}
}
