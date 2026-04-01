using Godot;

namespace Tts;

public partial class NeutralFleetNode : FleetNodeBase
{
	private const string NeutralVisualScenePath = "res://scenes/fleet/NeutralFleetNode.tscn";
	private const int CountFontSize = 11;

	protected override string? VisualScenePath => NeutralVisualScenePath;

	public void Initialize(float systemRadius, float gap, float radius, float labelWidth, float labelHeight, float outlineWidth, Color fill, Color outline)
	{
		BaseInitialize(systemRadius, gap, radius, labelWidth, labelHeight, CountFontSize);
		if (GetIconSprite() is Sprite2D sprite)
			sprite.Modulate = fill;
	}
}
