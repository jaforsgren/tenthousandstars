using Godot;
using System;

namespace Tts;

public partial class TransitFleetNode : Node2D
{
	[Export] public float DotRadius { get; set; } = 5f;

	private Color _dotColor;
	private Action? _onArrive;

	public void Launch(Vector2 from, Vector2 to, Color color, float durationSeconds, Action onArrive)
	{
		_dotColor = color;
		_onArrive = onArrive;
		Position = from;
		QueueRedraw();

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

	public override void _Draw() => DrawCircle(Vector2.Zero, DotRadius, _dotColor);
}
