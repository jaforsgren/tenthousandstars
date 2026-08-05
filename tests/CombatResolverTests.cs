using Tts.Utils;
using Xunit;

namespace Tts.Tests;

public class CombatResolverTests
{
	[Fact]
	public void Resolve_AttackerWins_WhenRemainderHigher()
	{
		var result = CombatResolver.Resolve(100f, 60f, 1f);
		Assert.True(result.AttackerWins);
		Assert.Equal(40f, result.AttackerRemainder);
		Assert.Equal(-40f, result.DefenderRemainder);
	}

	[Fact]
	public void Resolve_DefenderBonus_BoostsDefender()
	{
		var result = CombatResolver.Resolve(100f, 80f, 2f);
		Assert.False(result.AttackerWins);
		Assert.Equal(-60f, result.AttackerRemainder);
		Assert.Equal(-20f, result.DefenderRemainder);
	}

	[Fact]
	public void Resolve_EqualStrengths_DefenderBonusTilts()
	{
		var result = CombatResolver.Resolve(50f, 50f, 1.2f);
		Assert.False(result.AttackerWins);
	}
}
