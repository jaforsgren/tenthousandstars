using System;
using Tts.Debug;
using Xunit;

namespace Tts.Tests;

public class CampaignSimulatorTests
{
	[Fact]
	public void ParseWinPattern_EmptyPattern_AllWins()
	{
		var result = CampaignSimulator.ParseWinPattern("", 3);
		Assert.Equal([true, true, true], result);
	}

	[Fact]
	public void ParseWinPattern_AppliesPattern()
	{
		var result = CampaignSimulator.ParseWinPattern("W,L,W", 5);
		Assert.Equal([true, false, true, true, true], result);
	}

	[Fact]
	public void ParseWinPattern_CaseInsensitive()
	{
		var result = CampaignSimulator.ParseWinPattern("w,l", 2);
		Assert.Equal([true, false], result);
	}
}
