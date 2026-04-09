using Godot;
using System;

namespace Tts;

public partial class RerouteButtonNode : Control
{
	private Button _button = null!;
	private Action? _onPressed;

	public override void _Ready()
	{
		_button = GetNode<Button>("%RerouteButton");
		_button.Pressed += () => _onPressed?.Invoke();
		_button.Position = new Vector2(-UILayout.ButtonSize / 2f, -UILayout.ButtonSize / 2f);
		Visible = false;
	}

	public void Configure(bool hasActiveRoute, Action onPressed)
	{
		_onPressed = onPressed;
		_button.ButtonPressed = hasActiveRoute;
		Visible = true;
	}
}
