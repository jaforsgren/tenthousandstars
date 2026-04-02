using System;
using System.Collections.Generic;

namespace Tts;

public class OutroGenerator
{
    private readonly OutroConfig _config;
    private readonly NarrativeDatabase _db;
    private readonly Random _rng;

    public OutroGenerator(OutroConfig config, NarrativeDatabase db, Random rng)
    {
        _config = config;
        _db = db;
        _rng = rng;
    }

    public (string Title, string Text) Generate(StoryState state)
    {
        var winTag = state.MissionsWon * 2 >= state.TotalChapters ? "win" : "loss";

        // Prefer archetype-specific template, fall back to generic win/loss, then anything
        var candidates = WithBothTags(_config.Templates, winTag, state.ArchetypeId);
        if (candidates.Count == 0) candidates = WithTag(_config.Templates, winTag);
        if (candidates.Count == 0) candidates = new List<OutroTemplate>(_config.Templates);

        var template = candidates[_rng.Next(candidates.Count)];

        var archetypeName = LookupArchetypeName(state.ArchetypeId);
        var text = ApplyTokens(template.Text, state, archetypeName);

        return (template.Title, text);
    }

    private string LookupArchetypeName(string archetypeId)
    {
        foreach (var a in _db.Archetypes)
            if (a.Id == archetypeId) return a.Name;
        return archetypeId;
    }

    private static string ApplyTokens(string text, StoryState state, string archetypeName)
        => text
            .Replace("{PlayerFaction}", state.Player.FactionName)
            .Replace("{EnemyFaction}", state.Enemy.FactionName)
            .Replace("{MissionsWon}", state.MissionsWon.ToString())
            .Replace("{TotalMissions}", state.TotalChapters.ToString())
            .Replace("{ArchetypeName}", archetypeName);

    private static List<OutroTemplate> WithBothTags(OutroTemplate[] templates, string tagA, string tagB)
    {
        var result = new List<OutroTemplate>();
        foreach (var t in templates)
            if (HasTag(t.Tags, tagA) && HasTag(t.Tags, tagB))
                result.Add(t);
        return result;
    }

    private static List<OutroTemplate> WithTag(OutroTemplate[] templates, string tag)
    {
        var result = new List<OutroTemplate>();
        foreach (var t in templates)
            if (HasTag(t.Tags, tag))
                result.Add(t);
        return result;
    }

    private static bool HasTag(string[] tags, string tag)
    {
        foreach (var t in tags)
            if (t == tag) return true;
        return false;
    }
}
