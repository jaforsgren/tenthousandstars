namespace Tts.Config;

public record ScenarioConfig(ScenarioDefinition[] Scenarios);

public record ScenarioDefinition(
    string Id,
    string Title,
    string IntroText,
    ScenarioStage[] Stages,
    ScenarioCriteria Criteria);

public record ScenarioStage(
    ScenarioChoice[] Choices,
    string Text = "",
    string? DependsOn = null);

public record ScenarioChoice(
    string Label,
    string Outcome);

public record ScenarioCriteria(
    int MinMissionsPlayed = 0,
    int? MinMissionsWon = null,
    int? MinMissionsLost = null);
