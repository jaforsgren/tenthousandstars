using Godot;

namespace Tts;

public partial class SystemCircleNode : Node2D
{
	private float _radius;
	private Color _fill;
	private Color _outline;
	private float _outlineWidth;

	private const int ArcSegments = 64;

	private const string VisualScenePath = "res://scenes/SystemCircleNode.tscn";

	public override void _Ready()
	{
		var scene = GD.Load<PackedScene>(VisualScenePath);
		if (scene != null)
			AddChild(scene.Instantiate());
	}

	public void Initialize(float radius, Color fill, Color outline, float outlineWidth)
	{
		_radius = radius;
		_fill = fill;
		_outline = outline;
		_outlineWidth = outlineWidth;
	}

	public override void _Draw()
	{
		DrawCircle(Vector2.Zero, _radius, _fill);
		DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, ArcSegments, _outline, _outlineWidth);
	}
}
