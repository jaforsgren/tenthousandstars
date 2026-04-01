namespace Tts;

public record Character(
    AiDisposition Disposition,
    string Title,
    string FactionName,
    string Description,
    string[] Barks);
