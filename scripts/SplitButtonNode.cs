using Godot;
using System;

namespace Tts;

public partial class SplitButtonNode : Control
{
	private const float ButtonSize = 48f;
	private const float ButtonMargin = 20f;
	private const float Gap = 8f;
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
		_button.Position = new Vector2(
			viewportSize.X - ButtonSize * Slot - ButtonMargin - Gap * (Slot - 1),
			viewportSize.Y - ButtonSize - ButtonMargin
		);
		Visible = true;
	}
}
