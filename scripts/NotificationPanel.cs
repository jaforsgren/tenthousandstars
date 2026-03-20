using System;
using Godot;

namespace Tts;

public partial class NotificationPanel : PanelContainer
{
	private const float PanelWidth = 240f;
	private const float TopPadding = 12f;

	private Label _titleLabel = null!;
	private Label _descriptionLabel = null!;

	private float _timeRemaining;
	private Action? _onDismiss;
	private bool _active;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(PanelWidth, 0f);

		var vbox = new VBoxContainer();
		AddChild(vbox);

		_titleLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_titleLabel.AddThemeFontSizeOverride("font_size", 13);
		vbox.AddChild(_titleLabel);

		_descriptionLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_descriptionLabel.AddThemeFontSizeOverride("font_size", 10);
		vbox.AddChild(_descriptionLabel);

		Visible = false;
	}

	private bool _allowEarlyDismiss;

	public void Show(string title, string description, float displaySeconds, Vector2 viewportSize, Action onDismiss, bool allowEarlyDismiss = true)
	{
		_titleLabel.Text = title;
		_descriptionLabel.Text = description;
		_timeRemaining = displaySeconds;
		_onDismiss = onDismiss;
		_allowEarlyDismiss = allowEarlyDismiss;
		_active = true;

		var x = (viewportSize.X - PanelWidth) / 2f;
		Position = new Vector2(x, TopPadding);
		Visible = true;
	}

	public override void _Process(double delta)
	{
		if (!_active)
			return;

		_timeRemaining -= (float)delta;
		if (_timeRemaining <= 0f)
			Dismiss();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_active || !_allowEarlyDismiss)
			return;

		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			Dismiss();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Dismiss()
	{
		if (!_active)
			return;

		_active = false;
		Visible = false;

		var callback = _onDismiss;
		_onDismiss = null;
		callback?.Invoke();
	}
}
