namespace Tts.Narrative;

public class ChapterGenerator : IChapterGenerator
{
    public ChapterContext GenerateChapter(ChapterDefConfig def, StoryState state)
        => new(def.Id, def.Title, def.IntroBarks, def.OutroBarks);
}
