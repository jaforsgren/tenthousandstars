namespace Tts;

public record ArchetypeConfig(
    string Id,
    string Name,
    string[] ChapterSequence,
    AiDisposition PlayerDisposition,
    string[] OpeningBarks);
