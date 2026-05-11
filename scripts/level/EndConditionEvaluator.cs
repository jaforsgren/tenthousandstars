using System.Collections.Generic;
using System.Linq;
using Tts.Config;
using Tts.Nodes;

namespace Tts.Level;

internal static class EndConditionEvaluator
{
	internal static bool IsVictoryMet(
		EndCondition condition,
		IReadOnlyList<SystemNode> systems,
		int objectiveSystemIndex,
		SystemOwner targetPlayerOwner,
		int defendSystemIndex)
	{
		if (condition.EnemiesLeft.HasValue)
		{
			var enemiesWithFleet = systems.Count(s => !s.IsPlayerOwned && s.HasFleet);
			if (enemiesWithFleet > condition.EnemiesLeft.Value)
				return false;
		}

		if (condition.SystemsLeft.HasValue)
		{
			var nonPlayerSystems = systems.Count(s => !s.IsPlayerOwned);
			if (nonPlayerSystems > condition.SystemsLeft.Value)
				return false;
		}

		if (condition.TargetSystemHops.HasValue)
		{
			if (objectiveSystemIndex < 0 || !systems[objectiveSystemIndex].IsPlayerOwned)
				return false;
		}

		if (condition.EliminateTargetPlayer == true)
		{
			if (targetPlayerOwner == SystemOwner.None)
				return false;
			if (systems.Any(s => s.OwnerPlayer == targetPlayerOwner && s.HasFleet))
				return false;
		}

		if (condition.DefendObjectiveSystem == true)
		{
			if (defendSystemIndex < 0 || !systems[defendSystemIndex].IsPlayerOwned)
				return false;
		}

		return true;
	}

	internal static bool IsDefeatByAbandonedDefend(
		EndCondition? condition,
		IReadOnlyList<SystemNode> systems,
		int defendSystemIndex)
	{
		return condition?.DefendObjectiveSystem == true
			&& defendSystemIndex >= 0
			&& !systems[defendSystemIndex].IsPlayerOwned;
	}

	internal static bool IsDefeatByElimination(IReadOnlyList<SystemNode> systems)
		=> !systems.Any(s => s.IsPlayerOwned);
}
