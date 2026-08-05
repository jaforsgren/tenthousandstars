using Tts.Commitment;
using Tts.Level;
using Xunit;

namespace Tts.Tests;

public class CommitmentStateTests
{
	private static CommitmentState MakeState() => new()
	{
		SystemIndex = 0,
		Owner = SystemOwner.Player,
		Intent = IntentType.Attack,
		Influences = new FleetInfluences(5f, 5f, 5f, 5f),
	};

	[Fact]
	public void IsActive_TrueWhenNeitherCompleteNorInterrupted()
	{
		Assert.True(MakeState().IsActive);
	}

	[Fact]
	public void IsActive_FalseWhenComplete()
	{
		var state = MakeState();
		state.IsComplete = true;
		Assert.False(state.IsActive);
	}

	[Fact]
	public void IsActive_FalseWhenInterrupted()
	{
		var state = MakeState();
		state.IsInterrupted = true;
		Assert.False(state.IsActive);
	}
}
