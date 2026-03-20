namespace Tts;

public record EndCondition(int? EnemiesLeft, int? SystemsLeft, string Description, string EndDescription);

public record EndStateConfig(float MissionBriefSeconds, float EndStateSeconds, EndCondition[] Conditions);
