namespace Tts.Utils;

// Central registry of scene resource paths to avoid stringly-typed duplication.
public static class ScenePaths
{
    public const string Level = "res://scenes/Level.tscn";
    public const string MainMenu = "res://scenes/ui/MainMenu.tscn";

    public const string FleetNode = "res://scenes/fleet/FleetNode.tscn";
    public const string TransitFleetNode = "res://scenes/fleet/TransitFleetNode.tscn";

    public const string CombatEffectNode = "res://scenes/effects/CombatEffectNode.tscn";

    public const string NarrativePanel = "res://scenes/narrative/NarrativePanel.tscn";

    public const string PlanetNode = "res://scenes/system/PlanetNode.tscn";
    public const string ProductionArcNode = "res://scenes/system/ProductionArcNode.tscn";
    public const string RerouteArrowNode = "res://scenes/system/RerouteArrowNode.tscn";
    public const string ScenarioBadgeNode = "res://scenes/system/ScenarioBadgeNode.tscn";
    public const string SystemCircleNode = "res://scenes/system/SystemCircleNode.tscn";
    public const string CommitmentIndicatorNode = "res://scenes/system/CommitmentIndicatorNode.tscn";

    public const string StoryDebugScene = "res://scenes/debug/StoryDebugScene.tscn";
    public const string NarrativeDebugRoot = "res://scenes/debug/NarrativeDebugRoot.tscn";
}
