namespace Tts.Narrative;

public enum GameMode { Skirmish, Story }

public record GameModeConfig(GameMode Mode);
