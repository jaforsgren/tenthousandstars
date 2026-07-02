using System;
using System.Linq;
using System.Text;
using Tts.Ai;
using Tts.Config;
using Tts.Narrative;
using Tts.Utils;

namespace Tts.Debug;

public static class NarrativeCli
{
    // Usage:
    //   (no args)             — full campaign with all wins, includes eligible scenarios per mission
    //   <archetype> [pattern] — full campaign for archetype (e.g. "conquest W,L,W")
    //   -v / --verbose        — also print Yarn text for each interlude and outro
    //   scenarios             — list all scenarios and their criteria, no campaign
    //   scenarios <played> <won> <lost> — list scenarios eligible at given mission stats
    public static void Run(string[] args)
    {
        var verbose = Array.IndexOf(args, "-v") >= 0 || Array.IndexOf(args, "--verbose") >= 0;
        args = Array.FindAll(args, a => a != "-v" && a != "--verbose");

        if (args.Length > 0 && args[0] == "scenarios")
        {
            RunScenariosStandalone(args);
            return;
        }

        RunCampaign(args, verbose);
    }

    private static void RunCampaign(string[] args, bool verbose)
    {
        var rng = new Random();
        var archetype = args.Length > 0 ? args[0] : "";
        var pattern = args.Length > 1 ? args[1] : "";

        var controller = NarrativeController.Create(rng);
        controller.StartCampaign(archetype);

        var aiNamingCfg = ConfigLoader.Load<AiNamingConfig>("res://config/ai_naming.json");
        var enemy = AiNaming.GenerateCharacter(aiNamingCfg, AiDisposition.Strategic, rng);
        controller.UpdateEnemy(enemy);

        var state = controller.CurrentState;
        var wins = ParseWinPattern(pattern, state.TotalChapters);

        var narrativePool = verbose
            ? YarnLinePool.Load($"res://yarn/narrative/{state.ArchetypeId}.yarn")
            : null;

        var sb = new StringBuilder();
        var lastSectorName = "";
        var lastDate = "";

        sb.AppendLine($"CAMPAIGN: {state.ArchetypeId}");
        sb.AppendLine($"Faction: {state.Player.FactionName}");
        sb.AppendLine();

        for (var i = 0; i < state.TotalChapters; i++)
        {
            var ctx = controller.GetNextMission();
            if (ctx.SectorName != null) lastSectorName = ctx.SectorName;
            if (ctx.InterludeDate != null) lastDate = ctx.InterludeDate;
            var won = wins[i];

            sb.AppendLine($"--- Chapter {i + 1}: {ctx.Chapter.ChapterId} ---");

            if (ctx.InterludeNodeName != null)
            {
                sb.AppendLine($"INTERLUDE: {ctx.InterludeNodeName}");
                sb.AppendLine($"  sector={ctx.SectorName}  date={ctx.InterludeDate}");
                if (narrativePool != null)
                    AppendYarnText(sb, narrativePool, ctx.InterludeNodeName, ctx.SectorName, ctx.InterludeDate, state.Player.FactionName, enemy.FactionName, ctx.Condition.Description);
                sb.AppendLine();
            }

            sb.AppendLine("MISSION:");
            sb.AppendLine(ctx.Condition.Description);
            sb.AppendLine();

            sb.AppendLine("BRIEFING:");
            sb.AppendLine(ctx.Briefing);
            sb.AppendLine();

            AppendScenarios(sb, ctx.Scenarios);

            sb.AppendLine(won ? "WIN" : "LOSS");
            sb.AppendLine();

            var playerSys = won ? 8 : 3;
            var enemySys = won ? 2 : 7;

            controller.OnMissionComplete(won, playerSys, enemySys, enemySys / 2);
        }

        if (controller.IsCampaignComplete)
        {
            var outroNode = controller.GetOutroNodeName();
            sb.AppendLine($"OUTRO: {outroNode}");
            if (narrativePool != null)
                AppendYarnText(sb, narrativePool, outroNode, lastSectorName, lastDate, state.Player.FactionName, enemy.FactionName,
                    missionsWon: wins.Count(w => w), totalMissions: state.TotalChapters);
        }

        Console.WriteLine(sb.ToString());
    }

    private static void RunScenariosStandalone(string[] args)
    {
        var played = args.Length > 1 ? int.Parse(args[1]) : 0;
        var won    = args.Length > 2 ? int.Parse(args[2]) : 0;

        var rng = new Random();
        var controller = NarrativeController.Create(rng);
        controller.StartCampaign();

        var eligible = controller.SelectEligibleScenarios(played, won);

        var sb = new StringBuilder();
        sb.AppendLine($"SCENARIOS (missions played={played} won={won} lost={played - won}):");
        sb.AppendLine();

        if (eligible.Length == 0)
        {
            sb.AppendLine("  (none eligible)");
        }
        else
        {
            foreach (var s in eligible)
                AppendScenarioDetail(sb, s);
        }

        Console.WriteLine(sb.ToString());
    }

    private static void AppendScenarios(StringBuilder sb, ScenarioDefinition[] scenarios)
    {
        if (scenarios.Length == 0) return;

        sb.AppendLine("SCENARIOS:");
        foreach (var s in scenarios)
        {
            sb.AppendLine($"  [{s.Id}] {s.Title}");
            sb.AppendLine($"  Intro: {s.IntroText}");
            sb.AppendLine($"  Stages: {s.Stages.Length}");
            foreach (var stage in s.Stages)
            {
                var dep = stage.DependsOn == null ? "(entry)" : $"after \"{stage.DependsOn}\"";
                var choiceList = stage.Choices.Length > 0
                    ? string.Join(", ", System.Array.ConvertAll(stage.Choices, c => $"\"{c.Outcome}\""))
                    : "(terminal)";
                sb.AppendLine($"    {dep} → {choiceList}");
            }
        }
        sb.AppendLine();
    }

    private static void AppendScenarioDetail(StringBuilder sb, ScenarioDefinition s)
    {
        sb.AppendLine($"[{s.Id}] {s.Title}");
        sb.AppendLine($"  Criteria: played>={s.Criteria.MinMissionsPlayed}" +
            (s.Criteria.MinMissionsWon.HasValue ? $", won>={s.Criteria.MinMissionsWon}" : "") +
            (s.Criteria.MinMissionsLost.HasValue ? $", lost>={s.Criteria.MinMissionsLost}" : ""));
        sb.AppendLine($"  Intro: {s.IntroText}");
        sb.AppendLine($"  Stage tree:");
        foreach (var stage in s.Stages)
        {
            var dep = stage.DependsOn == null ? "(entry)" : $"DependsOn=\"{stage.DependsOn}\"";
            if (stage.Choices.Length == 0)
            {
                sb.AppendLine($"    {dep} → (terminal, no choices)");
            }
            else
            {
                foreach (var c in stage.Choices)
                    sb.AppendLine($"    {dep} → choice \"{c.Label}\" (outcome: {c.Outcome})");
            }
        }
        sb.AppendLine();
    }

    private static void AppendYarnText(
        StringBuilder sb,
        System.Collections.Generic.Dictionary<string, string[]> pool,
        string nodeName,
        string? sectorName,
        string? date,
        string playerFaction,
        string enemyFaction,
        string missionObjective = "",
        int missionsWon = 0,
        int totalMissions = 0)
    {
        var lines = YarnLinePool.GetPool(pool, nodeName);
        if (lines.Length == 0) return;

        sb.AppendLine();
        foreach (var raw in lines)
        {
            var line = raw
                .Replace("{$player_faction}", playerFaction)
                .Replace("{$enemy_faction}", enemyFaction)
                .Replace("{$sector_name}", sectorName ?? "")
                .Replace("{$date}", date ?? "")
                .Replace("{$mission_objective}", missionObjective)
                .Replace("{$missions_won}", missionsWon.ToString())
                .Replace("{$total_missions}", totalMissions.ToString());

            // Strip "Narrator: " prefix
            var text = line.StartsWith("Narrator: ", StringComparison.Ordinal)
                ? line["Narrator: ".Length..]
                : line;

            if (!string.IsNullOrWhiteSpace(text))
                sb.AppendLine($"  {text}");
        }
    }

    public static bool[] ParseWinPattern(string input, int chapters)
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
