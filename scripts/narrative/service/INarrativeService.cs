using Tts.Config;
namespace Tts.Narrative;

public interface INarrativeService
{
    StoryState CurrentState { get; }
    bool IsCampaignComplete { get; }
    void StartCampaign(string archetypeId);
    void UpdateEnemy(Character enemy);
    MissionContext GetNextMission();
    void OnMissionComplete(MissionResult result);
    ScenarioDefinition[] SelectEligibleScenarios(StoryState state);
}
