using System;
using System.Collections.Generic;
using Tts.Narrative;

namespace Tts.Debug;

public readonly record struct CampaignMission(CampaignNode Node, bool Won);

// Walks a CampaignController's mission graph with a win/loss pattern,
// recording each mission and its outcome. Shared by the CLI and the story debug scene.
public static class CampaignSimulator
{
    public static IReadOnlyList<CampaignMission> Run(CampaignController campaign, string rawPattern)
    {
        var missions = new List<CampaignMission>();
        if (rawPattern == null)
            rawPattern = "";

        while (!campaign.IsCampaignComplete && !campaign.Current.IsTerminal)
        {
            var node = campaign.Current;
            var won = WantsWin(rawPattern, missions.Count);
            missions.Add(new CampaignMission(node, won));
            campaign.Advance(won);
        }

        return missions;
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

    private static bool WantsWin(string rawPattern, int missionIndex)
    {
        var parts = rawPattern.ToUpperInvariant().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;
        var idx = Math.Min(missionIndex, parts.Length - 1);
        return parts[idx] != "L";
    }
}
