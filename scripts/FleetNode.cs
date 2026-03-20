using Godot;

namespace Tts;

public partial class FleetNode : Node2D
{
	private float _radius;
	private Color _playerFill;
	private Color _playerOutline;
	private Color _neutralFill;
	private Color _neutralOutline;
	private float _outlineWidth;
	private Label _label = null!;

	private float _ships;
	private SystemOwner _owner;
	private bool _selected;

	private const float SelectedOutlineWidthMultiplier = 4f;
	private const int ArcSegments = 32;
	private const int FontSize = 11;
	private const string VisualScenePath = "res://scenes/FleetNode.tscn";

	public override void _Ready()
	{
		var scene = GD.Load<PackedScene>(VisualScenePath);
		if (scene != null)
			AddChild(scene.Instantiate());
	}

	public void Initialize(
		float systemRadius,
		float gap,
		float radius,
		float labelWidth,
		float labelHeight,
		Color playerFill,
		Color playerOutline,
		Color neutralFill,
		Color neutralOutline,
		float outlineWidth)
	{
		_radius = radius;
		_playerFill = playerFill;
		_playerOutline = playerOutline;
		_neutralFill = neutralFill;
		_neutralOutline = neutralOutline;
		_outlineWidth = outlineWidth;

		Position = new Vector2(0f, systemRadius + gap + radius);

		_label = new Label
		{
			Position = new Vector2(-labelWidth / 2f, -labelHeight / 2f),
			Size = new Vector2(labelWidth, labelHeight),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Text = "0",
			Visible = false
		};
		_label.AddThemeColorOverride("font_color", Colors.White);
		_label.AddThemeFontSizeOverride("font_size", FontSize);
		AddChild(_label);

		Visible = false;
	}

	public bool ContainsPoint(Vector2 worldPos)
		=> worldPos.DistanceTo(GlobalPosition) <= _radius;

	public void UpdateFleet(float ships, SystemOwner owner, bool selected)
	{
		_ships = ships;
		_owner = owner;
		_selected = selected;

		var hasFleet = ships > 0;
		Visible = hasFleet;
		if (hasFleet)
		{
			_label.Text = Mathf.FloorToInt(ships).ToString();
			_label.Visible = true;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_ships <= 0)
			return;

		var fill = _owner == SystemOwner.Player ? _playerFill : _neutralFill;
		var outline = _owner == SystemOwner.Player ? _playerOutline : _neutralOutline;
		DrawCircle(Vector2.Zero, _radius, fill);
		var outlineWidth = _selected ? _outlineWidth * SelectedOutlineWidthMultiplier : _outlineWidth;
		var outlineColor = _selected ? Colors.White : outline;
		DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, ArcSegments, outlineColor, outlineWidth);
	}
}
