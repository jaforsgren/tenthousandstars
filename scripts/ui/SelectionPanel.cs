using System;
using Godot;

namespace Tts;

[Tool]
public partial class SelectionPanel : PanelContainer
{
	private const float PanelWidth = 200f;
	private const float TopPadding = 100f;

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

		_titleLabel = GetNode<Label>("%Title");
		_descriptionLabel = GetNode<Label>("%Description");
		_scenarioSection = GetNode<VBoxContainer>("Content/ScenarioSection");
		_scenarioIntroLabel = GetNode<Label>("Content/ScenarioSection/ScenarioIntro");
		_enterButton = GetNode<Button>("Content/ScenarioSection/EnterButton");
		_ignoreButton = GetNode<Button>("Content/ScenarioSection/IgnoreButton");

		_enterButton.Pressed += () => _onEnterAction?.Invoke();
		_ignoreButton.Pressed += () => _onIgnoreAction?.Invoke();

		if (Engine.IsEditorHint())
		{
			ApplyEditorPreview();
			return;
		}

		Visible = false;
	}

	public void ShowAt(string title, string description, Vector2 viewportSize)
	{
		_titleLabel.Text = title;
		_descriptionLabel.Text = description;
		_scenarioSection.Visible = false;
		_onEnterAction = null;
		_onIgnoreAction = null;
		Visible = true;
		Position = new Vector2((viewportSize.X - PanelWidth) / 2f, TopPadding);
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
		Position = new Vector2((viewportSize.X - PanelWidth) / 2f, TopPadding);
	}

	private void ApplyEditorPreview()
	{
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
