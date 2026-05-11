using Godot;
using System;
using Tts.Utils;

namespace Tts.Ui;

public partial class InfoButton : Control
{
	private Button _button = null!;
	private Action? _onPressed;

	public override void _Ready()
	{
		_button = GetNode<Button>("%InfoButton");
		_button.Pressed += () => _onPressed?.Invoke();
		_button.Position = new Vector2(-UILayout.ButtonSize / 2f, -UILayout.ButtonSize / 2f);
		UILayout.ApplyGreyStyle(_button);
		Visible = false;
	}

	public void Configure(Action onPressed)
	{
		_onPressed = onPressed;
		Visible = true;
	}
}
