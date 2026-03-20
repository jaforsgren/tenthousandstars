using Godot;

namespace Tts;

public partial class RouteNode : Node2D
{
	private static readonly Color RouteColor = new(0.45f, 0.5f, 0.65f, 0.5f);
	private static readonly Color ScoutedModulate = new(0.5f, 0.55f, 0.65f, 0.45f);

	private Vector2 _from;
	private Vector2 _to;
	private float _routeWidth;

	// Positions are in Level (parent) local space; RouteNode sits at (0,0) so local == parent local.
	public void Initialize(Vector2 from, Vector2 to)
	{
		_from = from;
		_to = to;
	}

	public void SetFogState(FogState fogState)
	{
		Visible = fogState != FogState.Hidden;
		Modulate = fogState == FogState.Scouted ? ScoutedModulate : Colors.White;
	}

	public override void _Ready()
	{
		_routeWidth = ConfigLoader.Load<RouteConfig>("res://config/route.json").RouteWidth;
	}

	public override void _Draw() => DrawLine(_from, _to, RouteColor, _routeWidth);
}
