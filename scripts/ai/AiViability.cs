using System;
using Tts.Utils;

namespace Tts.Ai;

// Pure combat-viability prediction for AI dispositions — no Godot dependency.
public static class AiViability
{
    public static bool IsViableAttack(
        float attackerShips,
        float defenderShips,
        float defenderBonus,
        AiDisposition disposition,
        float strategicMinSpareShips,
        float cautiousAttackChance,
        Random rng)
    {
        var result = CombatResolver.Resolve(attackerShips, defenderShips, defenderBonus);
        return disposition switch
        {
            AiDisposition.Aggressive => result.AttackerWins,
            AiDisposition.Strategic  => result.AttackerWins && result.AttackerRemainder >= strategicMinSpareShips,
            AiDisposition.Cautious   => result.AttackerWins && (float)rng.NextDouble() < cautiousAttackChance,
            _ => false
        };
    }
}
