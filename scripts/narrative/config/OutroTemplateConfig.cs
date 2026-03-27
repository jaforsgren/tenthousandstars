namespace Tts;

public record OutroTemplate(
    string Id,
    string[] Tags,
    string Title,
    string Text);

public record OutroConfig(OutroTemplate[] Templates);
