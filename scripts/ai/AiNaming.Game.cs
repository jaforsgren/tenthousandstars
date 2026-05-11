using System;

namespace Tts.Ai;

public static partial class AiNaming
{
    public static AiPlayerData GenerateAiPlayer(AiNamingConfig cfg, SystemOwner owner, AiDisposition disposition, Random rng)
    {
        var (title, factionName, description, barks) = GenerateParts(cfg, disposition, rng);
        return new AiPlayerData(owner, disposition, title, factionName, description, barks);
    }
}
