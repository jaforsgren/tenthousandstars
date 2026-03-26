namespace Tts;

public interface INarrativeService
{
    StoryState CurrentState { get; }
    bool IsCampaignComplete { get; }
    void StartCampaign(string archetypeId);
    void UpdateEnemyFactionName(string enemyFaction);
    MissionContext GetNextMission();
    void OnMissionComplete(MissionResult result);
}
