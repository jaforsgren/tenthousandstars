using System;
using System.Linq;
using Godot;
using Tts.Config;

namespace Tts.Ui;

[Tool]
public partial class ScenarioPanel : Control
{
	private Label _titleLabel = null!;
	private Label _stageText = null!;
	private HBoxContainer _choiceContainer = null!;

	private ScenarioDefinition? _scenario;
	private Action? _onClose;

	public override void _Ready()
	{
		_titleLabel = GetNode<Label>("PanelBox/Layout/TitlePad/Title");
		_stageText = GetNode<Label>("PanelBox/Layout/ScrollArea/StageTextPad/StageText");
		_choiceContainer = GetNode<HBoxContainer>("PanelBox/Layout/ChoicesPad/Choices");
	}

	public void Show(ScenarioDefinition scenario, Vector2 viewportSize, Action onClose)
	{
		_scenario = scenario;
		_onClose = onClose;
		_titleLabel.Text = scenario.Title;
		Size = viewportSize;
		Visible = true;
		GameSpeed.PushUiPause();
		ShowStageForOutcome(null);
	}

	private void ShowStageForOutcome(string? outcome)
	{
		if (_scenario == null) return;

		var stage = _scenario.Stages.FirstOrDefault(s => s.DependsOn == outcome);
		if (stage == null)
		{
			Close();
			return;
		}

		_stageText.Text = stage.Text;

		foreach (var child in _choiceContainer.GetChildren())
			child.QueueFree();

		if (stage.Choices.Length > 0)
		{
			foreach (var choice in stage.Choices)
			{
				var btn = new Button { Text = choice.Label };
				var capturedOutcome = choice.Outcome;
				btn.Pressed += () => ShowStageForOutcome(capturedOutcome);
				_choiceContainer.AddChild(btn);
			}
		}
		else
		{
			var btn = new Button { Text = "Continue" };
			btn.Pressed += Close;
			_choiceContainer.AddChild(btn);
		}
	}

	private void Close()
	{
		Visible = false;
		GameSpeed.PopUiPause();
		var cb = _onClose;
		_onClose = null;
		_scenario = null;
		cb?.Invoke();
	}
}
