using Godot;

namespace Tts;

public partial class ScenarioBadgeNode : Node2D
{
	private Button _badge = null!;
	private float _height;

	private const float BadgeHorizontalOffset = 0.55f;
	private const float BadgeVerticalOffset = 1.15f;
	private static readonly Color BadgeFill = new(0.5f, 0.5f, 0.5f, 0.9f);
	private static readonly Color LineColor = new(1f, 1f, 1f, 0.35f);
	private const float LineWidth = 1.5f;
	private const int CornerRadius = 4;

	public override void _Ready()
	{
		_badge = GetNode<Button>("Badge");
		_height = _badge.CustomMinimumSize.Y;
	}

	public void Initialize(float systemRadius, float gap)
	{
		var badgeOffset = new Vector2(systemRadius * BadgeHorizontalOffset, -(systemRadius * BadgeVerticalOffset));
		Position = badgeOffset;

		var style = new StyleBoxFlat
		{
			BgColor = BadgeFill,
			CornerRadiusTopLeft = CornerRadius,
			CornerRadiusTopRight = CornerRadius,
			CornerRadiusBottomLeft = CornerRadius,
			CornerRadiusBottomRight = CornerRadius,
			CornerDetail = 4
		};
		_badge.AddThemeStyleboxOverride("normal", style);
		_badge.AddThemeStyleboxOverride("hover", style);
		_badge.AddThemeStyleboxOverride("pressed", style);
		_badge.AddThemeColorOverride("font_color", Colors.Black);

		// Line from badge bottom-centre toward the system circle edge
		var systemEdge = badgeOffset.Normalized() * (systemRadius + gap) - badgeOffset;

		var line = new Line2D
		{
			DefaultColor = LineColor,
			Width = LineWidth
		};
		line.AddPoint(new Vector2(0f, _height / 2f));
		line.AddPoint(systemEdge);
		AddChild(line);
	}
}
