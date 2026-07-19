using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using Tts.Narrative;

namespace Tts.Debug;

public partial class StoryDebugScene : Control
{
	private LineEdit _winPatternInput = null!;
	private Button _generateButton = null!;
	private Button _backButton = null!;
	private RichTextLabel _outputText = null!;

	private const string NarrativePanelPath = "res://scenes/narrative/NarrativePanel.tscn";

	public override void _Ready()
	{
		_winPatternInput = GetNode<LineEdit>("%WinPatternInput");
		_generateButton  = GetNode<Button>("%GenerateButton");
		_backButton      = GetNode<Button>("%BackButton");
		_outputText      = GetNode<RichTextLabel>("%OutputText");

		_generateButton.Pressed += OnGeneratePressed;
		_backButton.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/Level.tscn");
	}

	private void OnGeneratePressed()
	{
		try
		{
			_outputText.Text = BuildReport(_winPatternInput.Text.Trim());
		}
		catch (Exception ex)
		{
			_outputText.Text = $"[color=red][b]ERROR[/b][/color]\n{ex.Message}\n\n{ex.StackTrace}";
		}
	}

	private string BuildReport(string rawPattern)
	{
		var rng = new Random();
		var campaign = CampaignController.Load(rng);

		var sb = new StringBuilder();

		AppendHeader(sb, "CAMPAIGN");
		AppendField(sb, "Player", campaign.PlayerFaction);
		sb.AppendLine();

		var missionNodes = new List<CampaignNode>();
		var visited = new System.Collections.Generic.HashSet<string>();
		var node = campaign.Current;
		while (node != null && !node.IsTerminal && visited.Add(node.Title))
		{
			missionNodes.Add(node);
			node = node.OnWin != null ? GetNodeOrNull(campaign, node.OnWin) : null;
		}

		var wins = NarrativeCli.ParseWinPattern(rawPattern, missionNodes.Count);

		for (var i = 0; i < missionNodes.Count; i++)
		{
			var m = missionNodes[i];
			var won = wins[i];

			sb.AppendLine($"[color=cyan][b]── Mission {i + 1}: {m.Title} ──[/b][/color]");
			sb.AppendLine();

			AppendSubheader(sb, "INTERLUDE");
			AppendField(sb, "Yarn Node", m.Title, indent: true);
			sb.AppendLine();

			AppendSubheader(sb, "CONDITION");
			AppendField(sb, "Objective", m.Description, indent: true);
			if (m.TimeoutSeconds.HasValue)
				AppendField(sb, "Timeout", $"{m.TimeoutSeconds}s", indent: true);
			sb.AppendLine();

			var outcomeColor = won ? "lime" : "tomato";
			sb.AppendLine($"[color={outcomeColor}][b]▸ OUTCOME: {(won ? "WIN" : "LOSS")}[/b][/color]");
			sb.AppendLine($"  {(won ? m.WinText : "Mission failed.")}");
			AppendField(sb, "Next", won ? (m.OnWin ?? "(campaign ends)") : (m.OnLoss ?? "(campaign ends)"), indent: true);
			sb.AppendLine();

			campaign.Advance(won);
		}

		if (!campaign.IsCampaignComplete && campaign.Current.IsTerminal)
		{
			AppendHeader(sb, "OUTRO");
			AppendField(sb, "Yarn Node", campaign.Current.Title);
		}

		return sb.ToString();
	}

	private static CampaignNode? GetNodeOrNull(CampaignController campaign, string title)
	{
		try
		{
			var rng = new Random();
			var tmp = CampaignController.Load(rng);
			// Advance to the target node by name
			while (tmp.Current.Title != title && !tmp.Current.IsTerminal)
				tmp.Advance(won: true);
			return tmp.Current.Title == title ? tmp.Current : null;
		}
		catch { return null; }
	}

	private void PreviewYarnNode(string nodeName, IReadOnlyDictionary<string, string> vars)
	{
		var panel = GD.Load<PackedScene>(NarrativePanelPath).Instantiate<NarrativePanel>();
		AddChild(panel);
		panel.ShowNarrative(nodeName, vars, onComplete: panel.QueueFree);
	}

	private static void AppendHeader(StringBuilder sb, string text)
		=> sb.AppendLine($"[color=yellow][b]══ {text} ══[/b][/color]");

	private static void AppendSubheader(StringBuilder sb, string text)
		=> sb.AppendLine($"[color=green]▸ {text}[/color]");

	private static void AppendField(StringBuilder sb, string key, string value, bool indent = false)
	{
		var pad = indent ? "  " : "";
		sb.AppendLine($"{pad}  {key,-12} [b]{value}[/b]");
	}
}
