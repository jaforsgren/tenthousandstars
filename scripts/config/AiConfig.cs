using System.Collections.Generic;

namespace Tts;

public record AiConfig(
	int MinOpponents,
	int MaxOpponents,
	float ThinkIntervalSeconds,
	float StrategicMinSpareShips,
	float CautiousAttackChance,
	Dictionary<string, ColorData> DispositionColors,
	Dictionary<string, string[]> DispositionAbbreviations);
