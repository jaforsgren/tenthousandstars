namespace Tts.Effects;

public enum EffectKind { Buff, Debuff }

// How an ActiveEffect modifies game values.
// All deltas are additive; multipliers are multiplicative with each other.
public sealed record EffectPayload(
    float AttackerStrengthBonus = 0f,
    float DefenderBonusDelta = 0f);

public sealed record ActiveEffect(
    string Id,
    string Name,
    string Description,
    EffectKind Kind,
    EffectPayload Payload);
