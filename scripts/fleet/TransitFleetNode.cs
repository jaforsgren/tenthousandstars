using Godot;
using System;

namespace Tts;

// Color is applied via Modulate so the white Polygon2D takes the fleet's color.
public partial class TransitFleetNode : Node2D
{
	private Vector2 _to;
	private Action? _onArrive;
	private Tween? _tween;

	public void Launch(Vector2 from, Vector2 to, Color color, float durationSeconds, Action onArrive)
	{
		_to = to;
		Modulate = color;
		_onArrive = onArrive;
		Position = from;
		StartTween(durationSeconds);
	}

	public void InterruptAndRelaunch(Vector2 from, Vector2 to, float durationSeconds, Action onArrive)
	{
		_tween?.Kill();
		_to = to;
		_onArrive = onArrive;
		Position = from;
		StartTween(durationSeconds);
	}

	public void CancelInFlight()
	{
		_tween?.Kill();
		_onArrive = null;
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
		_onArrive?.Invoke();
		QueueFree();
	}
}
