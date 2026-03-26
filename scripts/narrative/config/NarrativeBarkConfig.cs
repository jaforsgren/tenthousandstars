namespace Tts;

public record NarrativeBark(
    string[] Tags,
    StateCondition? When,
    string Npc,
    string Message);

public record NarrativeBarkConfig(NarrativeBark[] Barks);
