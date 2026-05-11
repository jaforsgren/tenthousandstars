namespace Tts.Narrative;

public record ChapterDefConfig(
    string Id,
    string Title,
    string[] MissionTags,
    StateCondition? When,
    string[] IntroBarks,
    string[] OutroBarks);
