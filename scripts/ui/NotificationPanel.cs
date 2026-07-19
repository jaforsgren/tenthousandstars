using System;
using Godot;

namespace Tts.Ui;

[Tool]
public partial class NotificationPanel : PanelContainer
{
	private const float PanelWidth = 240f;
	private const float TopPadding = 12f;

	private Label _titleLabel = null!;
	private Label _descriptionLabel = null!;

	private ulong _startMs;
	private float _durationSeconds;
	private Action? _onDismiss;
	private bool _active;
	private bool _allowEarlyDismiss;

	private bool _previewInEditor;

	[Export]
	public bool PreviewInEditor
	{
		get => _previewInEditor;
		set
		{
			_previewInEditor = value;
			if (Engine.IsEditorHint() && IsNodeReady())
				ApplyEditorPreview();
		}
	}

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(PanelWidth, 0f);

		_titleLabel = GetNode<Label>("%Title");
		_descriptionLabel = GetNode<Label>("%Description");

		if (Engine.IsEditorHint())
		{
			ApplyEditorPreview();
			return;
		}
	}

	public void Show(string title, string description, float displaySeconds, Vector2 viewportSize, Action onDismiss, bool allowEarlyDismiss = true)
	{
		_titleLabel.Text = title;
		_descriptionLabel.Text = description;
		_startMs = Time.GetTicksMsec();
		_durationSeconds = displaySeconds;
		_onDismiss = onDismiss;
		_allowEarlyDismiss = allowEarlyDismiss;
		_active = true;

		var x = (viewportSize.X - PanelWidth) / 2f;
		// Position = new Vector2(x, TopPadding);
		Visible = true;
	}

	public override void _Process(double delta)
	{
		if (!_active)
			return;

		if ((Time.GetTicksMsec() - _startMs) / 1000f >= _durationSeconds)
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

	private void ApplyEditorPreview()
	{
		if (_titleLabel is null) return;
		if (_previewInEditor)
		{
			_titleLabel.Text = "Mission Brief";
			_descriptionLabel.Text = "Eliminate all enemy factions before they consolidate the outer systems.";
		}
		else
		{
			_titleLabel.Text = "Mission Title";
			_descriptionLabel.Text = "Mission description text goes here.";
		}
	}
}
