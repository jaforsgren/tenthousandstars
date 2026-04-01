namespace Tts;

public record OutroTemplateSource(string Id, string[] Tags, string Title, string TextFile);
public record OutroConfigSource(OutroTemplateSource[] Templates);

public record OutroTemplate(string Id, string[] Tags, string Title, string Text);
public record OutroConfig(OutroTemplate[] Templates);
