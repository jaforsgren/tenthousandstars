namespace Tts.Config;

public record SystemConfig(
	float SystemRadius,
	float FleetCircleGap,
	float BaseProduction,
	float PlanetProductionRate,
	float LabelWidth,
	float LabelHeight,
	ColorData SystemFill,
	ColorData SystemOutline,
	ColorData NeutralSystemOutline,
	float SystemOutlineWidth,
	float SunRadiusRatio,
	float PlanetOrbitSpeed,
	PlanetGradient[] PlanetGradients,
	ColorData FleetFill,
	ColorData FleetOutline,
	float FleetOutlineWidth,
	ColorData NeutralFleetFill,
	ColorData NeutralFleetOutline,
	ColorData CapitolFill,
	string[] SystemTexturePaths,
	float[] SystemTextureScales);
