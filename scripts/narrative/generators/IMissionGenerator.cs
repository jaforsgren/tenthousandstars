using System;

namespace Tts.Narrative;

public interface IMissionGenerator
{
    NarrativeConditionConfig SelectCondition(string[] requiredTags, StoryState state, Random rng, bool allRequired = true);
}
