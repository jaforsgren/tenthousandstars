namespace Tts;

public interface IBriefingGenerator
{
    string GenerateBriefing(NarrativeConditionConfig condition, ChapterContext chapter, StoryState state);
}
