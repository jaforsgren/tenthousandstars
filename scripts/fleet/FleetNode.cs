using Godot;

namespace Tts.Fleet;

public partial class FleetNode : Node2D
{
	private static readonly Color PlayerFill  = new(0.9f, 0.28f, 0.1f, 0.9f);
	private static readonly Color NeutralFill = new(0.5f, 0.5f,  0.5f, 0.9f);
	private static readonly Color AiFill      = new(0.5f, 0.5f,  0.5f, 0.9f);

	private static readonly Color LineColor = new(1f, 1f, 1f, 0.35f);
	private const float LineWidth    = 1.5f;
	private const int  CornerRadius  = 4;

	private float  _width;
	private float  _height;
	private Button _button      = null!;
	private Label  _factionLabel = null!;
	private bool   _hasFaction;

	public override void _Ready()
	{
		_button       = GetNode<Button>("%CountButton");
		_factionLabel = GetNode<Label>("%FactionLabel");
		_width        = _button.CustomMinimumSize.X;
		_height       = _button.CustomMinimumSize.Y;
		Visible       = false;
	}

	public bool ContainsPoint(Vector2 worldPos)
	{
		var local = worldPos - GlobalPosition;
		return Mathf.Abs(local.X) <= _width / 2f && Mathf.Abs(local.Y) <= _height / 2f;
	}

	public void InitializePlayer(float systemRadius, float gap)
		=> Setup(systemRadius, gap, PlayerFill, null, null);

	public void InitializeNeutral(float systemRadius, float gap)
		=> Setup(systemRadius, gap, NeutralFill, null, null);

	public void InitializeAi(float systemRadius, float gap, Color dispositionColor, string factionName)
		=> Setup(systemRadius, gap, AiFill, factionName, dispositionColor);

	private void Setup(float systemRadius, float gap, Color fill, string? factionName, Color? factionColor)
	{
		Position = new Vector2(0f, systemRadius + gap + _height / 2f);

		var style = new StyleBoxFlat
		{
			BgColor                  = fill,
			CornerRadiusTopLeft      = CornerRadius,
			CornerRadiusTopRight     = CornerRadius,
			CornerRadiusBottomLeft   = CornerRadius,
			CornerRadiusBottomRight  = CornerRadius,
			CornerDetail             = 4
		};
		_button.AddThemeStyleboxOverride("normal",  style);
		_button.AddThemeStyleboxOverride("hover",   style);
		_button.AddThemeStyleboxOverride("pressed", style);
		_button.AddThemeColorOverride("font_color", Colors.Black);

		var line = new Line2D { DefaultColor = LineColor, Width = LineWidth };
		line.AddPoint(new Vector2(0f, -_height / 2f));
		line.AddPoint(new Vector2(0f, -(gap + _height / 2f)));
		AddChild(line);

		_hasFaction = factionName != null;
		if (_hasFaction)
		{
			_factionLabel.Text = factionName!;
			_factionLabel.AddThemeColorOverride("font_color", factionColor!.Value);
		}
		_factionLabel.Visible = false;
	}

	public void UpdateFleet(float ships)
	{
		Visible = ships > 0;
		if (ships > 0)
			_button.Text = Mathf.FloorToInt(ships).ToString();
		if (_hasFaction)
			_factionLabel.Visible = ships > 0;
	}
}
