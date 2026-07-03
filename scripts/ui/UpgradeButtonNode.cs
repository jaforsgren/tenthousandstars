using Godot;
using System;
using Tts.Utils;

namespace Tts.Ui;

public partial class UpgradeButtonNode : Control
{
	private Button _button = null!;
	private Action? _onPressed;

	public override void _Ready()
	{
		_button = GetNode<Button>("%UpgradeButton");
		_button.Pressed += () => _onPressed?.Invoke();
		_button.Position = new Vector2(-UiStyles.ButtonSize / 2f, -UiStyles.ButtonSize / 2f);
		UiStyles.ApplyGreyStyle(_button);
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
