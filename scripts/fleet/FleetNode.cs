using Godot;

namespace Tts;

public abstract partial class FleetNodeBase : Node2D
{
	protected float _width;
	protected float _height;
	protected float _ships;
	protected bool _selected;
	protected Button _button = null!;

	private StyleBoxFlat _normalStyle = null!;
	private Color _outline;

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

	protected void BaseInitialize(float systemRadius, float gap, Color fill, Color outline)
	{
		_outline = outline;
		Position = new Vector2(0f, systemRadius + gap + _height / 2f);

		_normalStyle = new StyleBoxFlat { BgColor = fill, BorderColor = outline };
		_button.AddThemeStyleboxOverride("normal", _normalStyle);
		_button.AddThemeStyleboxOverride("hover", _normalStyle);
		_button.AddThemeStyleboxOverride("pressed", _normalStyle);
		_button.AddThemeColorOverride("font_color", Colors.White);
	}

	public virtual void UpdateFleet(float ships, bool selected)
	{
		_ships = ships;
		_selected = selected;
		var hasFleet = ships > 0;
		Visible = hasFleet;
		if (hasFleet)
			_button.Text = Mathf.FloorToInt(ships).ToString();
		_normalStyle.BorderColor = selected ? Colors.White : _outline;
	}
}
