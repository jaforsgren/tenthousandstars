namespace Tts.Config;

public sealed record GeneratorConfigs(
    LevelGeneratorConfig Generator,
    AiConfig Ai,
    AiNamingConfig AiNaming,
    SystemConfig System);

public static class GeneratorConfigLoader
{
    public static GeneratorConfigs Load()
        => new(
            Generator: ConfigLoader.Load<LevelGeneratorConfig>("res://config/level_generator.json"),
            Ai: ConfigLoader.Load<AiConfig>("res://config/ai.json"),
            AiNaming: ConfigLoader.Load<AiNamingConfig>("res://config/ai_naming.json"),
            System: ConfigLoader.Load<SystemConfig>("res://config/system.json"));
}
