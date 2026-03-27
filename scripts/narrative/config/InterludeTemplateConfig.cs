namespace Tts;

public record InterludeTemplate(
    string Id,
    string[] Tags,
    string Text);

public record InterludeConfig(
    int BaseYear,
    string DateFormat,
    string[] SectorNames,
    InterludeTemplate[] Templates);
