using Godot;
using System;

namespace Tts;

public partial class RerouteButtonNode : Control
{
	private const int Slot = 2;

	private Button _button = null!;
	private Action? _onPressed;

	public override void _Ready()
	{
		_button = GetNode<Button>("%RerouteButton");
		_button.Pressed += () => _onPressed?.Invoke();
		Visible = false;
	}

	public void ShowFor(Vector2 viewportSize, bool hasActiveRoute, Action onPressed)
	{
		_onPressed = onPressed;
		_button.Text = hasActiveRoute ? "Cancel\nRoute" : "Set\nRoute";
		_button.Position = UILayout.BottomRightButtonPosition(viewportSize, Slot);
		Visible = true;
	}

	public void UpdateRouteState(bool hasActiveRoute)
	{
		_button.Text = hasActiveRoute ? "Cancel\nRoute" : "Set\nRoute";
	}
}
