namespace Tts;

public record MissionContext(
    NarrativeConditionConfig Condition,
    string Briefing,
    ChapterContext Chapter,
    StoryState State,
    StoryText? Interlude,
    ScenarioDefinition[] Scenarios);
