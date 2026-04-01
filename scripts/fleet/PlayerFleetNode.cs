using Godot;

namespace Tts;

public partial class PlayerFleetNode : FleetNodeBase
{
	private const string PlayerVisualScenePath = "res://scenes/fleet/FleetNode.tscn";
	private const int CountFontSize = 11;

	protected override string? VisualScenePath => PlayerVisualScenePath;

	public void Initialize(float systemRadius, float gap, float radius, float labelWidth, float labelHeight, float outlineWidth, Color fill, Color outline)
	{
		BaseInitialize(systemRadius, gap, radius, labelWidth, labelHeight, CountFontSize);
		if (GetIconSprite() is Sprite2D sprite)
			sprite.Modulate = fill;
	}
}
