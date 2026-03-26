namespace Tts;

public record ArchetypeConfig(
    string Id,
    string Name,
    string[] ChapterSequence,
    string PlayerFactionName,
    string[] OpeningBarks);
