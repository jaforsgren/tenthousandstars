namespace Tts.Narrative;

public enum GameMode { Random, Story }

public record GameModeConfig(GameMode Mode);
