namespace Tts;

public record SystemConfig(
	float SystemRadius,
	float FleetCircleRadius,
	float FleetCircleGap,
	float BaseProduction,
	float LabelWidth,
	float LabelHeight,
	ColorData SystemFill,
	ColorData SystemOutline,
	float SystemOutlineWidth,
	ColorData PlanetFill,
	ColorData PlanetOutline,
	float PlanetOutlineWidth,
	ColorData FleetFill,
	ColorData FleetOutline,
	float FleetOutlineWidth,
	ColorData NeutralFleetFill,
	ColorData NeutralFleetOutline);
