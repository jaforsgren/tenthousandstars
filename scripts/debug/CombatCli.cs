using System;
using Tts.Utils;

namespace Tts.Debug;

public static class CombatCli
{
    // Usage: combat <attacker> <defender> [defenderBonus]
    //   attacker      — fleet size (e.g. 10)
    //   defender      — fleet size (e.g. 8)
    //   defenderBonus — multiplier applied to defender strength (default: 1.2)
    public static void Run(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run combat <attacker> <defender> [defenderBonus]");
            return;
        }

        if (!float.TryParse(args[0], out var attacker) || !float.TryParse(args[1], out var defender))
        {
            Console.Error.WriteLine("ERROR: attacker and defender must be numbers");
            return;
        }

        var bonus = args.Length > 2 && float.TryParse(args[2], out var b) ? b : 1.2f;

        var result = CombatResolver.Resolve(attacker, defender, bonus);

        Console.WriteLine();
        Console.WriteLine($"  Attacker fleet  : {attacker}");
        Console.WriteLine($"  Defender fleet  : {defender}");
        Console.WriteLine($"  Defender bonus  : {bonus:F2}x");
        Console.WriteLine();
        Console.WriteLine($"  Outcome         : {(result.AttackerWins ? "ATTACKER WINS" : "DEFENDER HOLDS")}");
        Console.WriteLine($"  Attacker left   : {result.AttackerRemainder:F1}");
        Console.WriteLine($"  Defender left   : {result.DefenderRemainder:F1}");
        Console.WriteLine();
    }
}
