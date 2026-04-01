using System.Collections.Generic;

namespace Tts;

public record AiFactionNameEntry(string Noun, string Adjective);

public record AiConfig(
    int MinOpponents,
    int MaxOpponents,
    float ThinkIntervalSeconds,
    float StrategicMinSpareShips,
    float CautiousAttackChance,
    Dictionary<string, ColorData> DispositionColors);

public record AiNamingConfig(
    Dictionary<string, float> DispositionReinforceChance,
    Dictionary<string, string[]> DispositionDescriptions,
    Dictionary<string, string[]> DispositionBarks,
    Dictionary<string, string[]> DispositionAbbreviations,
    AiFactionNameEntry[] Names,
    Dictionary<string, string[]> Suffix);
