using Godot;
using System;

namespace Tts;

public partial class UpgradeButtonNode : Control
{
	private const float InfoButtonSize = 48f;
	private const float InfoButtonMargin = 20f;
	private const float Gap = 8f;

	private Button _button = null!;
	private Action? _onPressed;

	public override void _Ready()
	{
		_button = GetNode<Button>("%UpgradeButton");
		_button.Pressed += () => _onPressed?.Invoke();
		Visible = false;
	}

	public void ShowFor(Vector2 viewportSize, int slotFromRight, string label, bool disabled, Action? onPressed)
	{
		_onPressed = onPressed;
		_button.Text = label;
		_button.Disabled = disabled;
		_button.Position = new Vector2(
			viewportSize.X - InfoButtonSize * slotFromRight - InfoButtonMargin - Gap * (slotFromRight - 1),
			viewportSize.Y - InfoButtonSize - InfoButtonMargin
		);
		Visible = true;
	}
}
