using System.Collections.Generic;

namespace Tts.Config;

public sealed record EncounterConfig
{
    public Dictionary<string, string[]> EventPools { get; init; } = new();
}
