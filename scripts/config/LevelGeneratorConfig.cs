namespace Tts;

public record LevelGeneratorConfig(
	int MinSystems,
	int MaxSystems,
	float MinSpacing,
	float Margin,
	float PlanetProductionRate,
	float MinOrbit,
	float MaxOrbit,
	float MinPlanetSize,
	float MaxPlanetSize,
	int MaxPlacementAttempts,
	int MaxConnectionsPerSystem);
