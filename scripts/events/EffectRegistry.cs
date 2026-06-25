using System;
using System.Collections.Generic;

namespace Tts.Events;

// Holds active buffs/debuffs for the current level.
// Set as Instance when the level creates it; cleared on level teardown.
public sealed class EffectRegistry
{
    public static EffectRegistry? Instance { get; private set; }

    private readonly List<ActiveEffect> _active = [];

    public IReadOnlyList<ActiveEffect> Effects => _active;

    public event Action? Changed;

    public EffectRegistry() => Instance = this;

    public void Apply(string id)
    {
        if (!Catalog.TryGetValue(id, out var effect)) return;
        _active.RemoveAll(e => e.Id == id);
        _active.Add(effect);
        Changed?.Invoke();
    }

    public void Remove(string id)
    {
        _active.RemoveAll(e => e.Id == id);
        Changed?.Invoke();
    }

    public float TotalAttackerStrengthBonus()
    {
        var total = 0f;
        foreach (var e in _active) total += e.Payload.AttackerStrengthBonus;
        return total;
    }

    public float TotalDefenderBonusDelta()
    {
        var total = 0f;
        foreach (var e in _active) total += e.Payload.DefenderBonusDelta;
        return total;
    }

    public float TotalEncounterChanceDelta()
    {
        var total = 0f;
        foreach (var e in _active) total += e.Payload.EncounterChanceDelta;
        return total;
    }

    // ── Effect catalog ────────────────────────────────────────────────────────

    public static readonly IReadOnlyDictionary<string, ActiveEffect> Catalog =
        new Dictionary<string, ActiveEffect>
        {
            ["blessed_weapons"] = new(
                "blessed_weapons",
                "Blessed Weapons",
                "Fleet consecrated by ancient rites. Attack strength +3.",
                EffectKind.Buff,
                new EffectPayload(AttackerStrengthBonus: 3f)),

            ["eldritch_corruption"] = new(
                "eldritch_corruption",
                "Eldritch Corruption",
                "Crew unsettled by visions from beyond. Attack strength −3.",
                EffectKind.Debuff,
                new EffectPayload(AttackerStrengthBonus: -3f)),

            ["horde_tribute"] = new(
                "horde_tribute",
                "Horde Tribute",
                "Tribute paid. Barbarian activity temporarily suppressed. Encounter chance −5%.",
                EffectKind.Buff,
                new EffectPayload(EncounterChanceDelta: -0.05f)),

            ["horde_aggression"] = new(
                "horde_aggression",
                "Horde Aggression",
                "Horde roused to fury. Encounter activity surges. Encounter chance +10%.",
                EffectKind.Debuff,
                new EffectPayload(EncounterChanceDelta: 0.10f)),

            ["relic_empowerment"] = new(
                "relic_empowerment",
                "Relic Empowerment",
                "Eldritch relic integrated into fleet systems. Attack +4, defender bonus −0.1.",
                EffectKind.Buff,
                new EffectPayload(AttackerStrengthBonus: 4f, DefenderBonusDelta: -0.1f)),

            ["ai_sympathy"] = new(
                "ai_sympathy",
                "AI Sympathy Network",
                "AI factions extend covert aid to your defense. Defender bonus +0.15.",
                EffectKind.Buff,
                new EffectPayload(DefenderBonusDelta: 0.15f)),

            ["system_compromise"] = new(
                "system_compromise",
                "System Compromise",
                "Sleeper code embedded in fleet command architecture. Attack strength −4.",
                EffectKind.Debuff,
                new EffectPayload(AttackerStrengthBonus: -4f)),

            ["ai_overclocked"] = new(
                "ai_overclocked",
                "Overclocked Protocols",
                "AI-optimised attack routines loaded. Attack strength +5.",
                EffectKind.Buff,
                new EffectPayload(AttackerStrengthBonus: 5f)),

            ["nemesis_intel"] = new(
                "nemesis_intel",
                "Nemesis Intel",
                "Tactical dossier on nemesis doctrine acquired. Attack strength +2.",
                EffectKind.Buff,
                new EffectPayload(AttackerStrengthBonus: 2f)),

            ["nemesis_curse"] = new(
                "nemesis_curse",
                "Nemesis Curse",
                "Command codes flagged by nemesis countermeasures. Defender bonus −0.2.",
                EffectKind.Debuff,
                new EffectPayload(DefenderBonusDelta: -0.2f)),

            ["inner_sanctum_key"] = new(
                "inner_sanctum_key",
                "Inner Sanctum Access",
                "Authentication codes for the nemesis home systems. Encounter chance −8%.",
                EffectKind.Buff,
                new EffectPayload(EncounterChanceDelta: -0.08f)),

            ["blood_price"] = new(
                "blood_price",
                "Blood Price",
                "The cost of passage was steep. Attack strength −5.",
                EffectKind.Debuff,
                new EffectPayload(AttackerStrengthBonus: -5f)),
        };
}
