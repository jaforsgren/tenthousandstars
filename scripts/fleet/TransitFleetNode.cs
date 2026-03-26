using Godot;
using System;

namespace Tts;

// Color is applied via Modulate so the white Polygon2D takes the fleet's color.
public partial class TransitFleetNode : Node2D
{
	private Action? _onArrive;

	public void Launch(Vector2 from, Vector2 to, Color color, float durationSeconds, Action onArrive)
	{
		Modulate = color;
		_onArrive = onArrive;
		Position = from;

		var tween = CreateTween();
		tween.TweenProperty(this, "position", to, durationSeconds)
			.SetTrans(Tween.TransitionType.Linear);
		tween.TweenCallback(Callable.From(OnTransitComplete));
	}

	private void OnTransitComplete()
	{
		_onArrive?.Invoke();
		QueueFree();
	}
}
