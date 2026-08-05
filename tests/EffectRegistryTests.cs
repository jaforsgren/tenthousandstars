using Tts.Effects;
using Xunit;

namespace Tts.Tests;

public class EffectRegistryTests
{
	[Fact]
	public void Apply_AddsEffect()
	{
		var registry = new EffectRegistry();
		registry.Apply("blessed_weapons");
		Assert.Single(registry.Effects);
		Assert.Equal("blessed_weapons", registry.Effects[0].Id);
	}

	[Fact]
	public void Apply_ReplacesDuplicate()
	{
		var registry = new EffectRegistry();
		registry.Apply("blessed_weapons");
		registry.Apply("blessed_weapons");
		Assert.Single(registry.Effects);
	}

	[Fact]
	public void Apply_UnknownId_IsIgnored()
	{
		var registry = new EffectRegistry();
		registry.Apply("does_not_exist");
		Assert.Empty(registry.Effects);
	}

	[Fact]
	public void Remove_RemovesEffect()
	{
		var registry = new EffectRegistry();
		registry.Apply("blessed_weapons");
		registry.Remove("blessed_weapons");
		Assert.Empty(registry.Effects);
	}

	[Fact]
	public void TotalAttackerStrengthBonus_SumsBuffAndDebuff()
	{
		var registry = new EffectRegistry();
		registry.Apply("blessed_weapons");    // +3
		registry.Apply("eldritch_corruption"); // -3
		registry.Apply("relic_empowerment");   // +4
		Assert.Equal(4f, registry.TotalAttackerStrengthBonus());
	}

	[Fact]
	public void TotalDefenderBonusDelta_SumsAll()
	{
		var registry = new EffectRegistry();
		registry.Apply("relic_empowerment"); // -0.1
		registry.Apply("ai_sympathy");       // +0.15
		Assert.Equal(0.05f, registry.TotalDefenderBonusDelta(), 4);
	}

	[Fact]
	public void Totals_AreZeroWithoutEffects()
	{
		var registry = new EffectRegistry();
		Assert.Equal(0f, registry.TotalAttackerStrengthBonus());
		Assert.Equal(0f, registry.TotalDefenderBonusDelta());
	}
}
