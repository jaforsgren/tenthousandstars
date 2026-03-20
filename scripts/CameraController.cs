using Godot;

namespace Tts;

public partial class CameraController : Camera2D
{
	private float _minZoom;
	private float _maxZoom;
	private float _zoomStep;
	private bool _dragging;

	public override void _Ready()
	{
		var cfg = ConfigLoader.Load<CameraConfig>("res://config/camera.json");
		_minZoom = cfg.MinZoom;
		_maxZoom = cfg.MaxZoom;
		_zoomStep = cfg.ZoomStep;
	}

	public void FocusOn(Vector2 worldPosition)
	{
		Position = worldPosition;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton)
			HandleMouseButton(mouseButton);
		else if (@event is InputEventMouseMotion mouseMotion && _dragging)
			Position -= mouseMotion.Relative / Zoom;
	}

	private void HandleMouseButton(InputEventMouseButton e)
	{
		switch (e.ButtonIndex)
		{
			case MouseButton.Right:
				_dragging = e.Pressed;
				break;
			case MouseButton.WheelUp when e.Pressed:
				Zoom = (Zoom + Vector2.One * _zoomStep).Clamp(Vector2.One * _minZoom, Vector2.One * _maxZoom);
				break;
			case MouseButton.WheelDown when e.Pressed:
				Zoom = (Zoom - Vector2.One * _zoomStep).Clamp(Vector2.One * _minZoom, Vector2.One * _maxZoom);
				break;
		}
	}
}
