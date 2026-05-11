using System;
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
    //   scenarios             — list all scenarios and their criteria, no campaign
    //   scenarios <played> <won> <lost> — list scenarios eligible at given mission stats
    public static void Run(string[] args)
    {
        if (args.Length > 0 && args[0] == "scenarios")
        {
            RunScenariosStandalone(args);
            return;
        }

        RunCampaign(args);
    }

    private static void RunCampaign(string[] args)
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

        var sb = new StringBuilder();

        sb.AppendLine($"CAMPAIGN: {state.ArchetypeId}");
        sb.AppendLine($"Faction: {state.Player.FactionName}");
        sb.AppendLine();

        for (var i = 0; i < state.TotalChapters; i++)
        {
            var ctx = controller.GetNextMission();
            var won = wins[i];

            sb.AppendLine($"--- Chapter {i + 1}: {ctx.Chapter.ChapterId} ---");

            if (ctx.Interlude != null)
            {
                sb.AppendLine("INTERLUDE:");
                if (!string.IsNullOrEmpty(ctx.Interlude.Title))
                    sb.AppendLine(ctx.Interlude.Title);
                sb.AppendLine(ctx.Interlude.Body);
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
            var outro = controller.GenerateOutro();
            sb.AppendLine("OUTRO:");
            sb.AppendLine(outro.Title);
            sb.AppendLine(outro.Body);
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
