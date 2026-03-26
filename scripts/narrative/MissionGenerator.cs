using System;
using System.Collections.Generic;

namespace Tts;

public class MissionGenerator : IMissionGenerator
{
    private readonly NarrativeDatabase _db;

    public MissionGenerator(NarrativeDatabase db) => _db = db;

    public NarrativeConditionConfig SelectCondition(string[] requiredTags, StoryState state, Random rng)
    {
        var candidates = new List<NarrativeConditionConfig>();
        foreach (var condition in _db.Conditions)
        {
            if (HasAnyTag(condition.Tags, requiredTags))
                candidates.Add(condition);
        }

        if (candidates.Count == 0)
            candidates.AddRange(_db.Conditions);

        return candidates[rng.Next(candidates.Count)];
    }

    private static bool HasAnyTag(string[] conditionTags, string[] requiredTags)
    {
        foreach (var required in requiredTags)
            foreach (var tag in conditionTags)
                if (tag == required) return true;
        return false;
    }
}
