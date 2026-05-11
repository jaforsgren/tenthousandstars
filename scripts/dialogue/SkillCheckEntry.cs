#nullable enable

using System.Threading.Tasks;
using Godot;
using YarnSpinnerGodot;

namespace Tts;

/// <summary>
/// A visually distinct dialogue stack entry for skill check results.
///
/// Expected scene structure (SkillCheckEntry.tscn):
///   PanelContainer (this node, SkillCheckEntry.cs)
///     VBoxContainer
///       HBoxContainer  "VBoxContainer/Header"
///         Label        "VBoxContainer/Header/SkillLabel"
///         Label        "VBoxContainer/Header/RollLabel"
///       Label          "VBoxContainer/Result"
/// </summary>
public partial class SkillCheckEntry : PanelContainer
{
	private const float FadeInDuration = 0.4f;

	private Label _skillLabel = null!;
	private Label _rollLabel = null!;
	private Label _resultLabel = null!;

	public override void _Ready()
	{
		_skillLabel = GetNode<Label>("VBoxContainer/Header/SkillLabel");
		_rollLabel = GetNode<Label>("VBoxContainer/Header/RollLabel");
		_resultLabel = GetNode<Label>("VBoxContainer/Result");

		_skillLabel.ThemeTypeVariation = "SkillName";
		_rollLabel.ThemeTypeVariation = "RollDetail";
		_resultLabel.ThemeTypeVariation = "SkillResult";
	}

	public async Task ShowAsync(SkillCheckResult result)
	{
		_skillLabel.Text = $"{result.Skill.ToUpperInvariant()} — Difficulty {result.Difficulty}";
		_rollLabel.Text = $"({result.Die1}+{result.Die2}) + {result.SkillValue} = {result.Total}";

		_resultLabel.Text = result.Success ? "SUCCESS" : "FAILURE";
		_resultLabel.AddThemeColorOverride("font_color", result.Success ? DialogueStyles.SkillSuccess : DialogueStyles.SkillFailure);

		Modulate = new Color(1f, 1f, 1f, 0f);
		await FadeInAsync();
	}

	private async Task FadeInAsync()
	{
		ulong startMs = Time.GetTicksMsec();

		while (IsInstanceValid(this))
		{
			float elapsed = (Time.GetTicksMsec() - startMs) / 1000f;
			if (elapsed >= FadeInDuration) break;

			Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(elapsed / FadeInDuration, 0f, 1f));
			await YarnTask.NextFrame();
		}

		if (IsInstanceValid(this))
			Modulate = new Color(1f, 1f, 1f, 1f);
	}
}
