using Godot;

namespace Tts;

public partial class RouteNode : Node2D
{
	private static readonly Color RouteColor = new(0.45f, 0.5f, 0.65f, 0.5f);
	private const float RouteWidth = 1.5f;

	private Vector2 _from;
	private Vector2 _to;

	// Positions are in Level (parent) local space; RouteNode sits at (0,0) so local == parent local.
	public void Initialize(Vector2 from, Vector2 to)
	{
		_from = from;
		_to = to;
	}

	public override void _Draw() => DrawLine(_from, _to, RouteColor, RouteWidth);
}
