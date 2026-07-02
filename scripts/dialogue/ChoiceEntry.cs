#nullable enable

using System;
using Godot;

namespace Tts.Dialogue;

/// <summary>
/// Renders player choices in the dialogue scroll stack.
/// Shows a "You" label, then option buttons with "→" prefix.
/// Collapses to the selected option text after a choice is made.
///
/// Expected scene structure (ChoiceEntry.tscn):
///   VBoxContainer (this node)
///     Label         "YouLabel"
///     VBoxContainer "ButtonsContainer"
///     ColorRect     "Divider"
/// </summary>
public partial class ChoiceEntry : VBoxContainer
{
	private Label _youLabel = null!;
	private VBoxContainer _buttonsContainer = null!;
	private TextureRect _divider = null!;

	public override void _Ready()
	{
		_youLabel = GetNode<Label>("YouLabel");
		_buttonsContainer = GetNode<VBoxContainer>("ButtonsContainer");
		_divider = GetNode<TextureRect>("Divider");

		_youLabel.ThemeTypeVariation = "YouLabel";
		_youLabel.Visible = false;
	}

	public void Setup(YarnOption[] options, Action<int> onSelected)
	{
		int index = 0;
		foreach (YarnOption option in options)
		{
			var button = new Button();
			button.Text = $"{index + 1}.  {option.Text}";
			button.Alignment = HorizontalAlignment.Left;

			int capturedId = option.DialogueOptionID;
			string capturedText = option.Text;
			button.Pressed += () =>
			{
				CollapseToSelection(capturedText);
				onSelected(capturedId);
			};

			_buttonsContainer.AddChild(button);
			index++;
		}

		if (_buttonsContainer.GetChildCount() > 0)
			(_buttonsContainer.GetChild(0) as Button)?.GrabFocus();
	}

	private void CollapseToSelection(string selectedText)
	{
		_youLabel.Visible = true;

		foreach (Node child in _buttonsContainer.GetChildren())
			child.QueueFree();

		var label = new RichTextLabel();
		label.BbcodeEnabled = true;
		label.FitContent = true;
		label.ScrollActive = false;
		label.Text = $"[color=#777777]| {selectedText}[/color]";
		_buttonsContainer.AddChild(label);
	}
}
