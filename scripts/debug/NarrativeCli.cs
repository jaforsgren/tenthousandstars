using System;
using System.Text;

namespace Tts;

public static class NarrativeCli
{
    public static void Run(string[] args)
    {
        var rng = new Random();

        var archetype = args.Length > 0 ? args[0] : "";
        var pattern = args.Length > 1 ? args[1] : "";


        Console.WriteLine("RUN 1");

        var controller = NarrativeController.Create(rng);

        Console.WriteLine("RUN 2");

        controller.StartCampaign(archetype);

        var aiNamingCfg = ConfigLoader.Load<AiNamingConfig>("res://config/ai_naming.json");
        var enemy = AiNaming.GenerateCharacter(aiNamingCfg, AiDisposition.Strategic, rng);
        controller.UpdateEnemy(enemy);

        var state = controller.CurrentState;
        var wins = ParseWinPattern(pattern, state.TotalChapters);

        var sb = new StringBuilder();

        sb.AppendLine($"CAMPAIGN: {state.ArchetypeId}");
        sb.AppendLine($"Faction: {state.Player.FactionName}");
        sb.AppendLine();

        for (int i = 0; i < state.TotalChapters; i++)
        {

            var ctx = controller.GetNextMission();
            var won = wins[i];

            sb.AppendLine($"--- Chapter {i + 1}: {ctx.Chapter.ChapterId} ---");

            if (ctx.Interlude != null)
            {
                sb.AppendLine("INTERLUDE:");
                sb.AppendLine(ctx.Interlude.Text);
                sb.AppendLine();
            }

            sb.AppendLine("MISSION:");
            sb.AppendLine(ctx.Condition.Description);
            sb.AppendLine();

            sb.AppendLine("BRIEFING:");
            sb.AppendLine(ctx.Briefing);
            sb.AppendLine();

            sb.AppendLine(won ? "WIN" : "LOSS");
            sb.AppendLine();

            var playerSys = won ? 8 : 3;
            var enemySys = won ? 2 : 7;

            controller.OnMissionComplete(won, playerSys, enemySys, enemySys / 2);
        }

        if (controller.IsCampaignComplete)
        {
            var (title, text) = controller.GenerateOutro();
            sb.AppendLine("OUTRO:");
            sb.AppendLine(title);
            sb.AppendLine(text);
        }

        Console.WriteLine(sb.ToString());
    }

    private static bool[] ParseWinPattern(string input, int chapters)
    {
        var result = new bool[chapters];
        if (string.IsNullOrEmpty(input))
        {
            Array.Fill(result, true);
            return result;
        }

        var parts = input.ToUpperInvariant().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < chapters; i++)
        {
            var idx = Math.Min(i, parts.Length - 1);
            result[i] = parts[idx] != "L";
        }
        return result;
    }
}
