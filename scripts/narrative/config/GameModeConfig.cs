namespace Tts;

public enum GameMode { Random, Story }

public record GameModeConfig(GameMode Mode);
