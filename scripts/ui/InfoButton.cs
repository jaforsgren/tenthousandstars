using Godot;
using System;

namespace Tts;

public partial class InfoButton : Control
{
	private Button _button = null!;
	private Action? _onPressed;

	public override void _Ready()
	{
		_button = GetNode<Button>("%InfoButton");
		_button.Pressed += () => _onPressed?.Invoke();
		_button.Position = new Vector2(-UILayout.ButtonSize / 2f, -UILayout.ButtonSize / 2f);
		Visible = false;
	}

	public void Configure(Action onPressed)
	{
		_onPressed = onPressed;
		Visible = true;
	}
}
