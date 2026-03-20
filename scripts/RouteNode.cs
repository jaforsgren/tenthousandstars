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

	private FogState _fogState = FogState.Revealed;

	public void SetFogState(FogState fogState, float clearSeconds)
	{
		var previousState = _fogState;
		_fogState = fogState;

		if (fogState == FogState.Hidden)
		{
			Visible = false;
			return;
		}

		var targetModulate = fogState == FogState.Scouted ? ScoutedModulate : Colors.White;
		Visible = true;

		if (previousState == FogState.Hidden)
			Modulate = new Color(targetModulate.R, targetModulate.G, targetModulate.B, 0f);

		var tween = CreateTween();
		tween.TweenProperty(this, "modulate", targetModulate, clearSeconds);
	}

	public override void _Ready()
	{
		_routeWidth = ConfigLoader.Load<RouteConfig>("res://config/route.json").RouteWidth;
	}

	public override void _Draw() => DrawLine(_from, _to, RouteColor, _routeWidth);
}
