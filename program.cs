using System;
using Tts;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("START"); // sanity check

        NarrativeCli.Run(args);

        Console.WriteLine("END"); // sanity check
    }
}
