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

        var missions = CampaignSimulator.Run(campaign, rawPattern);

        var sb = new StringBuilder();
        sb.AppendLine("CAMPAIGN");
        sb.AppendLine($"Player: {campaign.PlayerFaction}");
        sb.AppendLine($"Enemy:  {campaign.EnemyFaction}");
        sb.AppendLine();

        for (var i = 0; i < missions.Count; i++)
        {
            var node = missions[i].Node;
            var won = missions[i].Won;

            sb.AppendLine($"--- Mission {i + 1}: {node.Title} ---");
            sb.AppendLine($"Interlude node: {node.Title}");
            sb.AppendLine($"Objective: {node.Description}");
            if (node.TimeoutSeconds.HasValue) sb.AppendLine($"Timeout: {node.TimeoutSeconds}s");
            sb.AppendLine(won ? "WIN" : "LOSS");
            sb.AppendLine($"Next: {(won ? node.OnWin : node.OnLoss) ?? "(campaign ends)"}");
            sb.AppendLine();
        }

        if (!campaign.IsCampaignComplete && campaign.Current.IsTerminal)
            sb.AppendLine($"OUTRO: {campaign.Current.Title}");

        Console.WriteLine(sb.ToString());
    }
}
