using Godot;
using System;

namespace Tts;

public partial class InfoButton : Control
{
	private const float ButtonSize = 48f;
	private const float Margin = 20f;

	private Button _button = null!;
	private Action? _onPressed;

	public override void _Ready()
	{
		_button = new Button { Text = "i" };
		_button.CustomMinimumSize = new Vector2(ButtonSize, ButtonSize);
		_button.Pressed += () => _onPressed?.Invoke();
		AddChild(_button);
		Visible = false;
	}

	public void ShowFor(Vector2 viewportSize, Action onPressed)
	{
		_onPressed = onPressed;
		_button.Position = new Vector2(
			viewportSize.X - ButtonSize - Margin,
			viewportSize.Y - ButtonSize - Margin
		);
		Visible = true;
	}
}
