using System.Collections.Generic;

namespace Tts;

public record AiConfig(
	int MinOpponents,
	int MaxOpponents,
	float ThinkIntervalSeconds,
	float StrategicMinSpareShips,
	float CautiousAttackChance,
	Dictionary<string, float> DispositionReinforceChance,
	Dictionary<string, string[]> DispositionDescriptions,
	Dictionary<string, string[]> DispositionBarks,
	Dictionary<string, ColorData> DispositionColors,
	Dictionary<string, string[]> DispositionAbbreviations);
