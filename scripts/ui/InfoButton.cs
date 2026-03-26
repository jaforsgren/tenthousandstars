using Godot;
using System;

namespace Tts;

public partial class InfoButton : Control
{
	private const int Slot = 1;

	private Button _button = null!;
	private Action? _onPressed;

	public override void _Ready()
	{
		_button = new Button { Text = "i" };
		_button.CustomMinimumSize = new Vector2(UILayout.ButtonSize, UILayout.ButtonSize);
		_button.Pressed += () => _onPressed?.Invoke();
		AddChild(_button);
		Visible = false;
	}

	public void ShowFor(Vector2 viewportSize, Action onPressed)
	{
		_onPressed = onPressed;
		_button.Position = UILayout.BottomRightButtonPosition(viewportSize, Slot);
		Visible = true;
	}
}
