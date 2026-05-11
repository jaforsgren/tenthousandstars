namespace Tts.Narrative;

public interface IChapterGenerator
{
    ChapterContext GenerateChapter(ChapterDefConfig def, StoryState state);
}
