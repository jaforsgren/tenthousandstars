namespace Tts;

public record LorePool(string[] Titles, string[] Descriptions);

public record LoreConfig(LorePool PlayerFleet, LorePool NeutralFleet, LorePool System, LorePool Planet);
