namespace Tts;

public record Bark(string Npc, string Message);

public record BarkConfig(Bark[] PlayerMove, Bark[] PlayerAttack, Bark[] PlayerUnderAttack, Bark[] PlayerForge, Bark[] PlayerFortify);
