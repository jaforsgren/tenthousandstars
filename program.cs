using System;
using Tts.Debug;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "combat")
            CombatCli.Run(args[1..]);
        else
            NarrativeCli.Run(args);
    }
}
