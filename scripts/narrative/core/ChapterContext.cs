namespace Tts;

public record ChapterContext(
    string ChapterId,
    string ChapterTitle,
    string[] IntroBarks,
    string[] OutroBarks);
