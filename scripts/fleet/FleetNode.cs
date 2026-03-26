using Godot;

namespace Tts;

public abstract partial class FleetNodeBase : Node2D
{
	protected float _radius;
	protected float _outlineWidth;
	protected float _ships;
	protected bool _selected;
	protected Label _label = null!;

	private const float SelectedOutlineWidthMultiplier = 4f;
	protected const int ArcSegments = 32;

	protected virtual string? VisualScenePath => null;

	public override void _Ready()
	{
		if (VisualScenePath is null) return;
		var scene = GD.Load<PackedScene>(VisualScenePath);
		if (scene != null)
			AddChild(scene.Instantiate());
	}

	protected void BaseInitialize(float systemRadius, float gap, float radius, float labelWidth, float labelHeight, float outlineWidth, int fontSize)
	{
		_radius = radius;
		_outlineWidth = outlineWidth;
		Position = new Vector2(0f, systemRadius + gap + radius);

		_label = new Label
		{
			Position = new Vector2(-labelWidth / 2f, -labelHeight / 2f),
			Size = new Vector2(labelWidth, labelHeight),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Visible = false
		};
		_label.AddThemeColorOverride("font_color", Colors.White);
		_label.AddThemeFontSizeOverride("font_size", fontSize);
		AddChild(_label);
		Visible = false;
	}

	public bool ContainsPoint(Vector2 worldPos) => worldPos.DistanceTo(GlobalPosition) <= _radius;

	public virtual void UpdateFleet(float ships, bool selected)
	{
		_ships = ships;
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

	protected void DrawFleetCircle(Color fill, Color outline)
	{
		if (_ships <= 0) return;
		DrawCircle(Vector2.Zero, _radius, fill);
		var width = _selected ? _outlineWidth * SelectedOutlineWidthMultiplier : _outlineWidth;
		var color = _selected ? Colors.White : outline;
		DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, ArcSegments, color, width);
	}
}
