using Godot;
using System;

namespace Tts;

public partial class UpgradeButtonNode : Control
{
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
		_button.Position = UILayout.BottomRightButtonPosition(viewportSize, slotFromRight);
		Visible = true;
	}
}
