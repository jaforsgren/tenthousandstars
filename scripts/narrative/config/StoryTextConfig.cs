namespace Tts;

// Source types — loaded from JSON; TextFile paths resolved in NarrativeController.
public record StoryTextTemplateSource(string Id, string[] Tags, string? Title, string TextFile);
public record StoryTextConfigSource(int BaseYear, string DateFormat, string[] SectorNames, StoryTextTemplateSource[] Templates);

// Resolved types — used by generators after text files are loaded.
public record StoryTextTemplate(string Id, string[] Tags, string? Title, string Text);
public record StoryTextConfig(int BaseYear, string DateFormat, string[] SectorNames, StoryTextTemplate[] Templates);
