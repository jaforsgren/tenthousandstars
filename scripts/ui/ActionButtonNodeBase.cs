using Godot;
using System;
using Tts.Utils;

namespace Tts.Ui;

// Shared wrapper logic for the action-menu buttons.
// Subclasses only declare their button node path, optional grey style, and Configure signature.
public abstract partial class ActionButtonNodeBase : Control
{
	protected Button _button = null!;
	private Action? _onPressed;

	protected abstract NodePath ButtonNodePath { get; }
	protected virtual bool UseGreyStyle => true;

	public override void _Ready()
	{
		_button = GetNode<Button>(ButtonNodePath);
		_button.Pressed += () => _onPressed?.Invoke();
		_button.Position = new Vector2(-UiStyles.ButtonSize / 2f, -UiStyles.ButtonSize / 2f);
		if (UseGreyStyle) UiStyles.ApplyGreyStyle(_button);
		Visible = false;
	}

	protected void Show(Action? onPressed)
	{
		_onPressed = onPressed;
		Visible = true;
	}
}
