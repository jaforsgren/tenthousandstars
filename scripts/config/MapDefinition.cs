using System.Collections.Generic;
using Tts.Ai;
using Tts.Level;
using Tts.Types;

namespace Tts.Config;

// Deserialised from res://config/maps/<name>.json. Authored maps bypass random
// generation and produce a LevelData directly via LevelGenerator/LMapBuilder.
public sealed record MapDefinition(
    MapSystem[] Systems,
    MapRoute[] Routes,
    MapEvent[]? Events = null);

// When a map event fires the Yarn node is run as an in-game dialogue by the
// MapEventController. MaxFires limits how many times the event may trigger
// across a mission (default: unlimited).
public sealed record MapEvent(
    MapEventTrigger Trigger,
    string YarnNode,
    float DelaySeconds = 0f,
    int MaxFires = int.MaxValue);

// AttackDelayed/ConquerDelayed fire DelaySeconds after the corresponding
// player action; MissionStart fires DelaySeconds after the mission begins.
public enum MapEventTrigger
{
    MissionStart,
    Attack,
    Conquer,
    AttackDelayed,
    ConquerDelayed
}

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