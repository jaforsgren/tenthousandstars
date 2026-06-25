namespace Tts.Narrative;

public abstract class TagMatchingBase
{
    protected static bool HasTag(string[] tags, string tag)
    {
        foreach (var t in tags)
            if (t == tag) return true;
        return false;
    }

    protected static bool HasAnyTag(string[] tags, string[] matchTags)
    {
        foreach (var mt in matchTags)
            if (HasTag(tags, mt)) return true;
        return false;
    }

    protected static bool HasAllTags(string[] tags, string[] requiredTags)
    {
        foreach (var rt in requiredTags)
            if (!HasTag(tags, rt)) return false;
        return true;
    }
}
