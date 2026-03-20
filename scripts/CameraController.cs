using Godot;

namespace Tts;

public partial class CameraController : Camera2D
{
	private float _minZoom;
	private float _maxZoom;
	private float _zoomStep;
	private float _followZoom;
	private float _focusTransitionSeconds;
	private float _trackpadZoomSensitivity;
	private bool _dragging;
	private bool _isFollowing;
	private Tween? _activeTween;

	public bool IsFollowing => _isFollowing;

	public override void _Ready()
	{
		var cfg = ConfigLoader.Load<CameraConfig>("res://config/camera.json");
		_minZoom = cfg.MinZoom;
		_maxZoom = cfg.MaxZoom;
		_zoomStep = cfg.ZoomStep;
		_followZoom = cfg.FollowZoom;
		_focusTransitionSeconds = cfg.FocusTransitionSeconds;
		_trackpadZoomSensitivity = cfg.TrackpadZoomSensitivity;
	}

	public void FocusOn(Vector2 worldPosition, float zoom)
	{
		Position = worldPosition;
		Zoom = Vector2.One * zoom;
	}

	public void FollowSystem(Vector2 worldPosition)
	{
		_isFollowing = true;
		AnimateTo(worldPosition, _followZoom);
	}

	private void ExitFollowMode()
	{
		_isFollowing = false;
		_activeTween?.Kill();
		_activeTween = null;
	}

	private void AnimateTo(Vector2 position, float zoom)
	{
		_activeTween?.Kill();
		_activeTween = CreateTween().SetParallel();
		_activeTween.TweenProperty(this, "position", position, _focusTransitionSeconds)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		_activeTween.TweenProperty(this, "zoom", Vector2.One * zoom, _focusTransitionSeconds)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton)
			HandleMouseButton(mouseButton);
		else if (@event is InputEventMouseMotion mouseMotion && _dragging)
			Position -= mouseMotion.Relative / Zoom;
		else if (@event is InputEventMagnifyGesture magnify)
			HandleMagnifyGesture(magnify);
	}

	private void HandleMouseButton(InputEventMouseButton e)
	{
		switch (e.ButtonIndex)
		{
			case MouseButton.Right when e.Pressed:
				_dragging = true;
				ExitFollowMode();
				break;
			case MouseButton.Right:
				_dragging = false;
				break;
			case MouseButton.WheelUp when e.Pressed:
				ApplyZoomStep(_zoomStep);
				break;
			case MouseButton.WheelDown when e.Pressed:
				ApplyZoomStep(-_zoomStep);
				break;
		}
	}

	private void HandleMagnifyGesture(InputEventMagnifyGesture e)
	{
		var factor = Mathf.Pow(e.Factor, _trackpadZoomSensitivity);
		Zoom = (Zoom * factor).Clamp(Vector2.One * _minZoom, Vector2.One * _maxZoom);
	}

	private void ApplyZoomStep(float step)
	{
		Zoom = (Zoom + Vector2.One * step).Clamp(Vector2.One * _minZoom, Vector2.One * _maxZoom);
	}
}
