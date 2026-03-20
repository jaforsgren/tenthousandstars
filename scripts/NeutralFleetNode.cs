using Godot;

namespace Tts;

public partial class NeutralFleetNode : FleetNodeBase
{
	private Color _fill;
	private Color _outline;

	private const string NeutralVisualScenePath = "res://scenes/NeutralFleetNode.tscn";
	private const int CountFontSize = 11;

	protected override string? VisualScenePath => NeutralVisualScenePath;

	public void Initialize(float systemRadius, float gap, float radius, float labelWidth, float labelHeight, float outlineWidth, Color fill, Color outline)
	{
		_fill = fill;
		_outline = outline;
		BaseInitialize(systemRadius, gap, radius, labelWidth, labelHeight, outlineWidth, CountFontSize);
	}

	public override void _Draw() => DrawFleetCircle(_fill, _outline);
}
