using Godot;

namespace Tts;

// Color is applied via Modulate so the white Polygon2D takes the fleet's color.
public partial class TransitFleetNode : Node2D
{
	private Vector2 _to;
	private Tween? _tween;

	public event System.Action? Arrived;

	public void Launch(Vector2 from, Vector2 to, Color color, float durationSeconds)
	{
		_to = to;
		Modulate = color;
		Position = from;
		StartTween(durationSeconds);
	}

	public void InterruptAndRelaunch(Vector2 from, Vector2 to, float durationSeconds)
	{
		_tween?.Kill();
		_to = to;
		Position = from;
		StartTween(durationSeconds);
	}

	public void CancelInFlight()
	{
		_tween?.Kill();
		QueueFree();
	}

	private void StartTween(float durationSeconds)
	{
		_tween = CreateTween();
		_tween.TweenProperty(this, "position", _to, durationSeconds)
			.SetTrans(Tween.TransitionType.Linear);
		_tween.TweenCallback(Callable.From(OnTransitComplete));
	}

	private void OnTransitComplete()
	{
		Arrived?.Invoke();
		QueueFree();
	}
}
