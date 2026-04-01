using System;
using System.Collections.Generic;

namespace Tts;

public class InterludeGenerator : IInterludeGenerator
{
    private readonly InterludeConfig _config;
    private readonly Random _rng;
    private readonly HashSet<string> _shownIds = [];
    private string[] _activeTags;

    public InterludeGenerator(InterludeConfig config, Random rng, string archetypeId)
    {
        _config = config;
        _rng = rng;
        _activeTags = ["start", archetypeId];
    }

    public InterludeContent? TryGenerate(MissionContext ctx)
    {
        var candidates = new List<InterludeTemplate>();
        foreach (var template in _config.Templates)
        {
            if (_shownIds.Contains(template.Id)) continue;
            if (!HasAnyTag(template.Tags, _activeTags)) continue;
            candidates.Add(template);
        }

        if (candidates.Count == 0) return null;

        var chosen = candidates[_rng.Next(candidates.Count)];
        _shownIds.Add(chosen.Id);
        _activeTags = chosen.Tags;

        return new InterludeContent(ApplyTokens(chosen.Text, ctx));
    }

    private string ApplyTokens(string text, MissionContext ctx)
    {
        var missionIndex = ctx.State.MissionsCompleted + 1;
        var year = _config.BaseYear + ctx.State.MissionsCompleted;
        var date = _config.DateFormat
            .Replace("{MissionIndex}", missionIndex.ToString())
            .Replace("{Year}", year.ToString());
        var sector = _config.SectorNames[_rng.Next(_config.SectorNames.Length)];

        return text
            .Replace("{PlayerFaction}", ctx.State.Player.FactionName)
            .Replace("{EnemyFaction}", ctx.State.Enemy.FactionName)
            .Replace("{MissionObjective}", ctx.Condition.Description)
            .Replace("{SectorName}", sector)
            .Replace("{ChapterTitle}", ctx.Chapter.ChapterTitle)
            .Replace("{Date}", date);
    }

    private static bool HasAnyTag(string[] templateTags, string[] activeTags)
    {
        foreach (var active in activeTags)
            foreach (var tag in templateTags)
                if (tag == active) return true;
        return false;
    }
}
