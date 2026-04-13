using Godot;
using System;

namespace Tts;

public partial class UpgradeButtonNode : Control
{
	private Button _button = null!;
	private Action? _onPressed;

	public override void _Ready()
	{
		_button = GetNode<Button>("%UpgradeButton");
		_button.Pressed += () => _onPressed?.Invoke();
		_button.Position = new Vector2(-UILayout.ButtonSize / 2f, -UILayout.ButtonSize / 2f);
		UILayout.ApplyGreyStyle(_button);
		Visible = false;
	}

	public void Configure(bool isActive, bool disabled, Action? onPressed)
	{
		_onPressed = onPressed;
		_button.ButtonPressed = isActive;
		_button.Disabled = disabled;
		Visible = true;
	}
}
