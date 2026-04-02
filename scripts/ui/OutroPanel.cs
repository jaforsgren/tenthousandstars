using System;
using Godot;

namespace Tts;

[Tool]
public partial class OutroPanel : Control
{
	private Label _titleLabel = null!;
	private Label _summaryText = null!;
	private Button _newCampaignButton = null!;
	private Button _randomMissionsButton = null!;
	private Button _quitButton = null!;

	public event Action? NewCampaignPressed;
	public event Action? RandomMissionsPressed;
	public event Action? QuitPressed;

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
		_titleLabel = GetNode<Label>("%TitleLabel");
		_summaryText = GetNode<Label>("%SummaryText");
		_newCampaignButton = GetNode<Button>("%NewCampaignButton");
		_randomMissionsButton = GetNode<Button>("%RandomMissionsButton");
		_quitButton = GetNode<Button>("%QuitButton");

		_newCampaignButton.Pressed += () => NewCampaignPressed?.Invoke();
		_randomMissionsButton.Pressed += () => RandomMissionsPressed?.Invoke();
		_quitButton.Pressed += () => QuitPressed?.Invoke();

		if (Engine.IsEditorHint())
		{
			ApplyEditorPreview();
			return;
		}

		Visible = false;
	}

	public void Show(string title, string text, Vector2 viewportSize)
	{
		_titleLabel.Text = title;
		_summaryText.Text = text;
		Size = viewportSize;
		Visible = true;
	}

	private void ApplyEditorPreview()
	{
		if (!_previewInEditor) return;
		_titleLabel.Text = "From the Ashes";
		_summaryText.Text = "Commander,\n\nThey said The Last Command was finished. They were wrong.\n\nAfter 2 of 3 engagements, we stand. The Enemy retreats.\n\nWe did not just survive. We prevailed.";
	}
}
