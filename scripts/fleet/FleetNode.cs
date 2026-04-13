using Godot;

namespace Tts;

public abstract partial class FleetNodeBase : Node2D
{
	protected float _width;
	protected float _height;
	protected float _ships;
	protected Button _button = null!;

	private static readonly Color LineColor = new(1f, 1f, 1f, 0.35f);
	private const float LineWidth = 1.5f;
	private const int CornerRadius = 4;

	public override void _Ready()
	{
		_button = GetNode<Button>("%CountButton");
		_width = _button.CustomMinimumSize.X;
		_height = _button.CustomMinimumSize.Y;
		Visible = false;
	}

	public bool ContainsPoint(Vector2 worldPos)
	{
		var local = worldPos - GlobalPosition;
		return Mathf.Abs(local.X) <= _width / 2f && Mathf.Abs(local.Y) <= _height / 2f;
	}

	protected void BaseInitialize(float systemRadius, float gap, Color fill)
	{
		Position = new Vector2(0f, systemRadius + gap + _height / 2f);

		var style = new StyleBoxFlat
		{
			BgColor = fill,
			CornerRadiusTopLeft = CornerRadius,
			CornerRadiusTopRight = CornerRadius,
			CornerRadiusBottomLeft = CornerRadius,
			CornerRadiusBottomRight = CornerRadius,
			CornerDetail = 4
		};
		_button.AddThemeStyleboxOverride("normal", style);
		_button.AddThemeStyleboxOverride("hover", style);
		_button.AddThemeStyleboxOverride("pressed", style);
		_button.AddThemeColorOverride("font_color", Colors.Black);

		var line = new Line2D
		{
			DefaultColor = LineColor,
			Width = LineWidth
		};
		line.AddPoint(new Vector2(0f, -_height / 2f));
		line.AddPoint(new Vector2(0f, -(gap + _height / 2f)));
		AddChild(line);
	}

	public virtual void UpdateFleet(float ships)
	{
		_ships = ships;
		Visible = ships > 0;
		if (ships > 0)
			_button.Text = Mathf.FloorToInt(ships).ToString();
	}
}
