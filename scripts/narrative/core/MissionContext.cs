using Tts.Config;
namespace Tts.Narrative;

public record MissionContext(
    NarrativeConditionConfig Condition,
    string Briefing,
    ChapterContext Chapter,
    StoryState State,
    string? InterludeNodeName,
    string SectorName,
    string InterludeDate,
    ScenarioDefinition[] Scenarios);
