using Godot;
using System;

namespace Tts;

public partial class SplitButtonNode : Control
{
	private const int Slot = 5;

	private Button _button = null!;
	private Action? _onPressed;

	public override void _Ready()
	{
		_button = GetNode<Button>("%SplitButton");
		_button.Pressed += () => _onPressed?.Invoke();
		Visible = false;
	}

	public void ShowFor(Vector2 viewportSize, bool disabled, Action? onPressed)
	{
		_onPressed = onPressed;
		_button.Disabled = disabled;
		_button.Position = UILayout.BottomRightButtonPosition(viewportSize, Slot);
		Visible = true;
	}
}
