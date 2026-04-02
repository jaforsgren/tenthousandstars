using System.Linq;

namespace Tts;

public abstract class TagMatchingBase
{
    protected static bool HasAnyTag(string[] conditionTags, string[] requiredTags)
    {
        foreach (var required in requiredTags)
            foreach (var tag in conditionTags)
                if (tag == required) return true;
        return false;
    }

    protected static bool HasAllTags(string[] conditionTags, string[] requiredTags)
    {
        foreach (var required in requiredTags)
            if (!conditionTags.Contains(required)) return false;
        return true;
    }
}
