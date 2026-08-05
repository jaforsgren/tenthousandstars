using System.Collections.Generic;
using System.Globalization;
using Tts.Config;

namespace Tts.Narrative;

// One node in campaign.yarn. Mission nodes have at least on_win or on_loss.
// Terminal nodes (no routing) are shown as the campaign outro then end the campaign.
public record CampaignNode(
    string Title,
    int? LevelSeed,
    int? EnemiesLeft,
    int? SystemsLeft,
    int? TargetSystemHops,
    bool EliminateTargetPlayer,
    bool DefendObjectiveSystem,
    float? TimeoutSeconds,
    string Description,
    string WinText,
    string? OnWin,
    string? OnLoss,
    string? Map)
{
    public bool IsTerminal => OnWin == null && OnLoss == null;

    public EndCondition ToEndCondition() => new(
        EnemiesLeft:           EnemiesLeft,
        SystemsLeft:           SystemsLeft,
        TargetSystemHops:      TargetSystemHops,
        EliminateTargetPlayer: EliminateTargetPlayer ? true : null,
        DefendObjectiveSystem: DefendObjectiveSystem ? true : null,
        TimeoutSeconds:        TimeoutSeconds,
        Description:           Description,
        EndDescription:        WinText);

    internal static CampaignNode FromHeader(IReadOnlyDictionary<string, string> h) => new(
        Title:                 h["title"],
        LevelSeed:             Int(h, "level_seed"),
        EnemiesLeft:           Int(h, "enemies_left"),
        SystemsLeft:           Int(h, "systems_left"),
        TargetSystemHops:      Int(h, "target_system_hops"),
        EliminateTargetPlayer: Bool(h, "eliminate_target"),
        DefendObjectiveSystem: Bool(h, "defend"),
        TimeoutSeconds:        Float(h, "timeout"),
        Description:           Str(h, "description") ?? "",
        WinText:               Str(h, "win_text") ?? "",
        OnWin:                 Str(h, "on_win"),
        OnLoss:                Str(h, "on_loss"),
        Map:                   Str(h, "map"));

    private static int? Int(IReadOnlyDictionary<string, string> h, string key)
        => h.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : null;

    private static float? Float(IReadOnlyDictionary<string, string> h, string key)
        => h.TryGetValue(key, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : null;

    private static bool Bool(IReadOnlyDictionary<string, string> h, string key)
        => h.TryGetValue(key, out var v) && (v == "true" || v == "1");

    private static string? Str(IReadOnlyDictionary<string, string> h, string key)
        => h.TryGetValue(key, out var v) ? v : null;
}
