using Godot;

namespace Tts;

public partial class PlanetNode : Node2D
{
	private float _size;
	private Color _fill;
	private Color _outline;
	private float _outlineWidth;

	private const float MinTapRadius = 10f;
	private const int ArcSegments = 16;
	private const string VisualScenePath = "res://scenes/PlanetNode.tscn";

	public override void _Ready()
	{
		var scene = GD.Load<PackedScene>(VisualScenePath);
		if (scene != null)
			AddChild(scene.Instantiate());
	}

	public void Initialize(Planet planet, Color fill, Color outline, float outlineWidth)
	{
		_size = planet.Size;
		_fill = fill;
		_outline = outline;
		_outlineWidth = outlineWidth;
		Position = new Vector2(
			Mathf.Cos(planet.OrbitAngle) * planet.OrbitRadius,
			Mathf.Sin(planet.OrbitAngle) * planet.OrbitRadius
		);
	}

	public bool ContainsPoint(Vector2 worldPos)
		=> worldPos.DistanceTo(GlobalPosition) <= Mathf.Max(_size, MinTapRadius);

	public override void _Draw()
	{
		DrawCircle(Vector2.Zero, _size, _fill);
		DrawArc(Vector2.Zero, _size, 0f, Mathf.Tau, ArcSegments, _outline, _outlineWidth);
	}
}
