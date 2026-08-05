using System.Collections.Generic;
using Tts.Ai;
using Tts.Level;
using Tts.Types;

namespace Tts.Config;

// Deserialised from res://config/maps/<name>.json. Authored maps bypass random
// generation and produce a LevelData directly via LevelGenerator/LMapBuilder.
public sealed record MapDefinition(
    MapSystem[] Systems,
    MapRoute[] Routes);

public sealed record MapPoint(float X, float Y);

public sealed record MapSystem(
    MapPoint Position,
    SystemOwner Owner = SystemOwner.None,
    float InitialFleet = 0f,
    string? Name = null,
    string? Description = null,
    AiDisposition? Disposition = null,
    IReadOnlyList<Planet>? Planets = null);

public sealed record MapRoute(int From, int To);