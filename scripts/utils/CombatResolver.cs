namespace Tts;

public readonly record struct CombatResult(bool AttackerWins, float AttackerRemainder, float DefenderRemainder);

public static class CombatResolver
{
	// Both sides take simultaneous casualties.
	// Defender bonus multiplies their effective fighting strength.
	// Attacker wins when their remainder exceeds the defender's remainder.
	public static CombatResult Resolve(float attackerFleet, float defenderFleet, float defenderBonus)
	{
		var attackerRemainder = attackerFleet - defenderFleet * defenderBonus;
		var defenderRemainder = defenderFleet - attackerFleet;
		return new CombatResult(
			AttackerWins: attackerRemainder > defenderRemainder,
			AttackerRemainder: attackerRemainder,
			DefenderRemainder: defenderRemainder
		);
	}
}
