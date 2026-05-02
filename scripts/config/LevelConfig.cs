namespace Tts;

public record LevelConfig(
	int DefaultPreviewSeed,
	bool FogEnabled,
	float FogClearSeconds,
	float FadeOutSeconds,
	float TransitDurationSeconds,
	float DefenderBonus,
	float UpgradeCost,
	float ForgeProductionBonus,
	float FortifyDefenseBonusMultiplier);
