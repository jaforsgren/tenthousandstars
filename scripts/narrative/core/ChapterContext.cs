namespace Tts.Narrative;

public record ChapterContext(
    string ChapterId,
    string ChapterTitle,
    string[] IntroBarks,
    string[] OutroBarks);
