using System;
using Godot;

namespace Tts.Ui;

[Tool]
public partial class SelectionPanel : PanelContainer
{
	private const float PanelWidth = 200f;
	private const float TopPadding = 100f;

	private ScrollContainer _scrollArea = null!;
	private VBoxContainer _content = null!;
	private Label _titleLabel = null!;
	private Label _descriptionLabel = null!;
	private VBoxContainer _scenarioSection = null!;
	private Label _scenarioIntroLabel = null!;
	private Button _enterButton = null!;
	private Button _ignoreButton = null!;

	private Action? _onEnterAction;
	private Action? _onIgnoreAction;

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

		_scrollArea = GetNode<ScrollContainer>("ContentPad/ContentLayout/ScrollArea");
		_content = GetNode<VBoxContainer>("ContentPad/ContentLayout/ScrollArea/Content");
		_titleLabel = GetNode<Label>("%Title");
		_descriptionLabel = GetNode<Label>("%Description");
		_scenarioSection = GetNode<VBoxContainer>("ContentPad/ContentLayout/ScrollArea/Content/ScenarioSection");
		_scenarioIntroLabel = GetNode<Label>("ContentPad/ContentLayout/ScrollArea/Content/ScenarioSection/ScenarioIntro");
		_enterButton = GetNode<Button>("ContentPad/ContentLayout/ScrollArea/Content/ScenarioSection/EnterButton");
		_ignoreButton = GetNode<Button>("ContentPad/ContentLayout/ScrollArea/Content/ScenarioSection/IgnoreButton");

		_enterButton.Pressed += () => _onEnterAction?.Invoke();
		_ignoreButton.Pressed += () => _onIgnoreAction?.Invoke();

		if (Engine.IsEditorHint())
		{
			ApplyEditorPreview();
			return;
		}
	}

	public void ShowAt(string title, string description, Vector2 viewportSize)
	{
		_titleLabel.Text = title;
		_descriptionLabel.Text = description;
		_scenarioSection.Visible = false;
		_onEnterAction = null;
		_onIgnoreAction = null;
		Visible = true;
		Callable.From(() => ClampScrollHeight(viewportSize)).CallDeferred();
	}

	public void ShowWithScenario(
		string title,
		string description,
		string scenarioIntro,
		Action onEnter,
		Action onIgnore,
		Vector2 viewportSize)
	{
		_titleLabel.Text = title;
		_descriptionLabel.Text = description;
		_scenarioIntroLabel.Text = scenarioIntro;
		_onEnterAction = onEnter;
		_onIgnoreAction = onIgnore;
		_scenarioSection.Visible = true;
		Visible = true;
		Callable.From(() => ClampScrollHeight(viewportSize)).CallDeferred();
	}

	private void ClampScrollHeight(Vector2 viewportSize)
	{
		var maxHeight = viewportSize.Y - TopPadding - 40f;
		_scrollArea.CustomMinimumSize = new Vector2(0, Mathf.Min(_content.Size.Y, maxHeight));
	}

	private void ApplyEditorPreview()
	{
		if (_titleLabel is null) return;
		if (_previewInEditor)
		{
			_titleLabel.Text = "Test Prime";
			_descriptionLabel.Text = "A contested frontier world. Rich in ore deposits but scarred by old campaigns.";
		}
		else
		{
			_titleLabel.Text = "System Name";
			_descriptionLabel.Text = "Description text goes here.";
		}
	}
}
