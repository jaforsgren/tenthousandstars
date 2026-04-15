using Godot;

namespace Tts;

public partial class SystemCircleNode : Node2D
{
	private float _radius;
	private Color _fill;
	private Color _outline;
	private float _outlineWidth;
	private float _sunRadiusRatio;
	private ColorRect _sunVisual = null!;

	private const int ArcSegments = 64;
	private const string VisualScenePath = "res://scenes/system/SystemCircleNode.tscn";

	public override void _Ready()
	{
		var cfg = ConfigLoader.Load<SystemConfig>("res://config/system.json");
		_sunRadiusRatio = cfg.SunRadiusRatio;

		var scene = GD.Load<PackedScene>(VisualScenePath);
		if (scene == null) return;
		var visual = scene.Instantiate();
		AddChild(visual);
		_sunVisual = visual.GetNode<ColorRect>("SunVisual");
	}

	public void Initialize(float radius, Color fill, Color outline, float outlineWidth)
	{
		_radius = radius;
		_fill = fill;
		_outline = outline;
		_outlineWidth = outlineWidth;

		var sunRadius = _radius * _sunRadiusRatio;
		_sunVisual.Size = new Vector2(sunRadius * 2f, sunRadius * 2f);
		_sunVisual.Position = new Vector2(-sunRadius, -sunRadius);
	}

	public void SetOutline(Color outline)
	{
		_outline = outline;
		QueueRedraw();
	}

	public override void _Draw()
	{
		DrawCircle(Vector2.Zero, _radius, _fill);
		DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, ArcSegments, _outline, _outlineWidth);
	}
}
