namespace Tts.Narrative;

public record BriefingTemplate(
    string[] Tags,
    StateCondition? When,
    string Template);

public record BriefingConfig(BriefingTemplate[] Templates);
