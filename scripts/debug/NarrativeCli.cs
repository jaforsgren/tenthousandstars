using System;
using System.Text;
using Tts.Narrative;

namespace Tts.Debug;

// Usage:
//   (no args)       — full campaign with all wins
//   <win_pattern>   — simulate with win/loss pattern e.g. "W,L,W"
public static class NarrativeCli
{
    public static void Run(string[] args)
    {
        var rng = new Random();
        var campaign = CampaignController.Load(rng);
        var rawPattern = args.Length > 0 ? args[0] : "";

        var sb = new StringBuilder();
        sb.AppendLine("CAMPAIGN");
        sb.AppendLine($"Player: {campaign.PlayerFaction}");
        sb.AppendLine($"Enemy:  {campaign.EnemyFaction}");
        sb.AppendLine();

        var missionIndex = 0;
        var patternParts = string.IsNullOrEmpty(rawPattern)
            ? Array.Empty<string>()
            : rawPattern.ToUpperInvariant().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        while (!campaign.IsCampaignComplete && !campaign.Current.IsTerminal)
        {
            var node = campaign.Current;
            var patternIdx = Math.Min(missionIndex, patternParts.Length - 1);
            var won = patternParts.Length == 0 || patternParts[patternIdx] != "L";

            sb.AppendLine($"--- Mission {missionIndex + 1}: {node.Title} ---");
            sb.AppendLine($"Interlude node: {node.Title}");
            sb.AppendLine($"Objective: {node.Description}");
            if (node.TimeoutSeconds.HasValue) sb.AppendLine($"Timeout: {node.TimeoutSeconds}s");
            sb.AppendLine(won ? "WIN" : "LOSS");
            sb.AppendLine($"Next: {(won ? node.OnWin : node.OnLoss) ?? "(campaign ends)"}");
            sb.AppendLine();

            campaign.Advance(won);
            missionIndex++;
        }

        if (!campaign.IsCampaignComplete && campaign.Current.IsTerminal)
            sb.AppendLine($"OUTRO: {campaign.Current.Title}");

        Console.WriteLine(sb.ToString());
    }

    public static bool[] ParseWinPattern(string input, int missions)
    {
        var result = new bool[missions];
        if (string.IsNullOrEmpty(input))
        {
            Array.Fill(result, true);
            return result;
        }

        var parts = input.ToUpperInvariant().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < missions; i++)
        {
            var idx = Math.Min(i, parts.Length - 1);
            result[i] = parts[idx] != "L";
        }
        return result;
    }
}
