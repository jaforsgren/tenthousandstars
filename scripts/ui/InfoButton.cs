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
		_button = GetNode<Button>("%InfoButton");
		_button.Pressed += () => _onPressed?.Invoke();
		Visible = false;
	}

	public void ShowFor(Vector2 viewportSize, Action onPressed)
	{
		_onPressed = onPressed;
		_button.Position = UILayout.BottomRightButtonPosition(viewportSize, Slot);
		Visible = true;
	}
}
