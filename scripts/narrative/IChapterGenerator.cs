namespace Tts;

public interface IChapterGenerator
{
    ChapterContext GenerateChapter(ChapterDefConfig def, StoryState state);
}
