using Godot;

namespace Tts;

public abstract partial class FleetNodeBase : Node2D
{
	protected float _radius;
	protected float _ships;
	protected bool _selected;
	protected Label _label = null!;

	private Node2D? _visualRoot;

	protected virtual string? VisualScenePath => null;

	public override void _Ready()
	{
		if (VisualScenePath is null) return;
		var scene = GD.Load<PackedScene>(VisualScenePath);
		if (scene == null) return;
		_visualRoot = scene.Instantiate<Node2D>();
		AddChild(_visualRoot);
	}

	protected Sprite2D? GetIconSprite() => _visualRoot?.GetNodeOrNull<Sprite2D>("Fleet");

	protected void BaseInitialize(float systemRadius, float gap, float radius, float labelWidth, float labelHeight, int fontSize)
	{
		_radius = radius;
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
	}
}
