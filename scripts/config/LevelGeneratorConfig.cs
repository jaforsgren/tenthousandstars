namespace Tts.Config;

public record LevelGeneratorConfig(
	int MinSystems,
	int MaxSystems,
	float MinSpacing,
	float Margin,
	float SpawnWidth,
	float SpawnHeight,
	float MinOrbit,
	float MaxOrbit,
	float MinPlanetSize,
	float MaxPlanetSize,
	int MinPlanets,
	int MaxPlanets,
	int MaxExtraRoutes,
	int MaxPlacementAttempts,
	int MaxConnectionsPerSystem,
	int NeutralFleetMin,
	int NeutralFleetMax,
	int AiMinHopsFromPlayer);
