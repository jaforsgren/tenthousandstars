using Godot;
using Tts.Config;
using Tts.Utils;

namespace Tts.Nodes;

public partial class SystemCircleNode : Node2D
{
	private float            _radius;
	private Color            _fill;
	private Color            _outline;
	private float            _outlineWidth;
	private float            _sunRadiusRatio;
	private ColorRect        _sunVisual  = null!;
	private CollisionShape2D _clickShape = null!;

	private const int    ArcSegments    = 64;
	private const string VisualScenePath = ScenePaths.SystemCircleNode;

	public override void _Ready()
	{
		var cfg = ConfigLoader.Load<SystemConfig>("res://config/system.json");
		_sunRadiusRatio = cfg.SunRadiusRatio;

		var scene = GD.Load<PackedScene>(VisualScenePath);
		if (scene == null) return;

		var visual = scene.Instantiate();
		AddChild(visual);

		_sunVisual  = visual.GetNode<ColorRect>("SunVisual");
		_clickShape = visual.GetNode<CollisionShape2D>("ClickArea/ClickShape");

		var variantSprite = visual.GetNode<Sprite2D>("VariantVisual");
		if (cfg.SystemTexturePaths.Length > 0)
		{
			var idx     = GD.RandRange(0, cfg.SystemTexturePaths.Length - 1);
			var scale   = idx < cfg.SystemTextureScales.Length ? cfg.SystemTextureScales[idx] : 0.2f;
			variantSprite.Texture = GD.Load<Texture2D>(cfg.SystemTexturePaths[idx]);
			variantSprite.Scale   = new Vector2(scale, scale);
		}
	}

	public void Initialize(float radius, Color fill, Color outline, float outlineWidth)
	{
		_radius       = radius;
		_fill         = fill;
		_outline      = outline;
		_outlineWidth = outlineWidth;

		var sunRadius = _radius * _sunRadiusRatio;
		_sunVisual.Size     = new Vector2(sunRadius * 2f, sunRadius * 2f);
		_sunVisual.Position = new Vector2(-sunRadius, -sunRadius);

		((RectangleShape2D)_clickShape.Shape).Size = new Vector2(_radius * 2f, _radius * 2f);
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
