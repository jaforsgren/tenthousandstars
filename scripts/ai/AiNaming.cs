using System;
using Tts.Config;

namespace Tts.Ai;

public static partial class AiNaming
{
    public static Character GenerateCharacter(AiNamingConfig cfg, AiDisposition disposition, Random rng)
    {
        var (title, factionName, description, barks) = GenerateParts(cfg, disposition, rng);
        return new Character(disposition, title, factionName, description, barks);
    }

    internal static (string Title, string FactionName, string Description, string[] Barks) GenerateParts(
        AiNamingConfig cfg, AiDisposition disposition, Random rng)
    {
        var abbrevs = cfg.DispositionAbbreviations[disposition.ToString()];
        var title = abbrevs[rng.Next(abbrevs.Length)];

        var nameEntry = cfg.Names[rng.Next(cfg.Names.Length)];
        var suffixes = cfg.Suffix[disposition.ToString()];
        var suffix = suffixes[rng.Next(suffixes.Length)];
        var factionName = string.IsNullOrEmpty(suffix) ? nameEntry.Noun : $"{nameEntry.Adjective} {suffix}";

        var descriptions = cfg.DispositionDescriptions[disposition.ToString()];
        var description = descriptions[rng.Next(descriptions.Length)];

        var barks = cfg.DispositionBarks[disposition.ToString()];

        return (title, factionName, description, barks);
    }
}
