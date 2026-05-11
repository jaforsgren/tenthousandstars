using Godot;

namespace Tts.Nodes;

public partial class RouteNode : FogAwareNode
{
	private static readonly Color RouteColor = new(0.45f, 0.5f, 0.65f, 0.5f);

	private Vector2 _from;
	private Vector2 _to;
	private float _routeWidth;
	private Color? _ownerColor;

	// Positions are in Level (parent) local space; RouteNode sits at (0,0) so local == parent local.
	public void Initialize(Vector2 from, Vector2 to)
	{
		_from = from;
		_to = to;
	}

	public void SetOwnerColor(Color? color)
	{
		_ownerColor = color;
		QueueRedraw();
	}

	public override void _Ready()
	{
		_routeWidth = ConfigLoader.Load<UiConfig>("res://config/ui.json").RouteWidth;
	}

	public override void _Draw()
	{
		var color = _ownerColor.HasValue ? _ownerColor.Value.WithAlpha(0.5f) : RouteColor;
		DrawLine(_from, _to, color, _routeWidth);
	}
}
