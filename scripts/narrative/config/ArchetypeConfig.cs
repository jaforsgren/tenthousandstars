using Tts.Ai;
namespace Tts.Narrative;

public record ArchetypeConfig(
    string Id,
    string Name,
    string[] ChapterSequence,
    AiDisposition PlayerDisposition);
