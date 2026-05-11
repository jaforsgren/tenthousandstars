#nullable enable

using System;

namespace Tts.Dialogue;

/// <summary>
/// Resolves skill checks: rolls 2d6 + skill value against a difficulty.
/// Stateless — all randomness is isolated here.
/// </summary>
public static class SkillSystem
{
	private static readonly Random Rng = new();

	public static SkillCheckResult Roll(string skill, int skillValue, int difficulty)
	{
		int die1 = Rng.Next(1, 7);
		int die2 = Rng.Next(1, 7);
		int roll = die1 + die2;
		int total = roll + skillValue;

		return new SkillCheckResult(
			Skill: skill,
			Difficulty: difficulty,
			Die1: die1,
			Die2: die2,
			SkillValue: skillValue,
			Roll: roll,
			Total: total,
			Success: total >= difficulty
		);
	}
}
