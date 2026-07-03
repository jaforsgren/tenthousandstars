using Godot;
using System;
using Tts.Utils;

namespace Tts.Ui;

public partial class SplitButtonNode : Control
{
	private Button _button = null!;
	private Action? _onPressed;

	public override void _Ready()
	{
		_button = GetNode<Button>("%SplitButton");
		_button.Pressed += () => _onPressed?.Invoke();
		_button.Position = new Vector2(-UiStyles.ButtonSize / 2f, -UiStyles.ButtonSize / 2f);
		Visible = false;
	}

	public void Configure(bool disabled, Action? onPressed)
	{
		_onPressed = onPressed;
		_button.Disabled = disabled;
		Visible = true;
	}
}
