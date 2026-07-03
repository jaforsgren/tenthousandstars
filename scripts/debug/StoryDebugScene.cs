using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using Tts.Ai;
using Tts.Config;
using Tts.Narrative;
using Tts.Utils;

namespace Tts.Debug;

public partial class StoryDebugScene : Control
{
	private OptionButton _archetypeOption = null!;
	private LineEdit _winPatternInput = null!;
	private Button _generateButton = null!;
	private Button _backButton = null!;
	private HBoxContainer _interludePreviewButtons = null!;
	private Button _previewOutroButton = null!;
	private RichTextLabel _outputText = null!;

	private string? _outroNodeName;
	private Dictionary<string, string>? _outroVars;

	private static readonly string[] ArchetypeIds = ["falling_empire", "rising_power", "conquest"];
	private static readonly string[] ArchetypeLabels = ["Falling Empire", "Rising Power", "Conquest"];

	private const string NarrativePanelPath = "res://scenes/narrative/NarrativePanel.tscn";

	public override void _Ready()
	{
		_archetypeOption = GetNode<OptionButton>("%ArchetypeOption");
		_winPatternInput = GetNode<LineEdit>("%WinPatternInput");
		_generateButton = GetNode<Button>("%GenerateButton");
		_backButton = GetNode<Button>("%BackButton");
		_interludePreviewButtons = GetNode<HBoxContainer>("%InterludePreviewButtons");
		_previewOutroButton = GetNode<Button>("%PreviewOutroButton");
		_outputText = GetNode<RichTextLabel>("%OutputText");

		foreach (var label in ArchetypeLabels)
			_archetypeOption.AddItem(label);
		_archetypeOption.AddItem("Random");
		_archetypeOption.Select(0);

		_generateButton.Pressed += OnGeneratePressed;
		_backButton.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/Level.tscn");
		_previewOutroButton.Pressed += PreviewOutro;
		_previewOutroButton.Disabled = true;
	}

	private void OnGeneratePressed()
	{
		_outroNodeName = null;
		_outroVars = null;
		_previewOutroButton.Disabled = true;

		foreach (Node child in _interludePreviewButtons.GetChildren())
			child.QueueFree();

		var idx = _archetypeOption.Selected;
		var archetypeId = idx < ArchetypeIds.Length ? ArchetypeIds[idx] : "";

		try
		{
			_outputText.Text = BuildReport(archetypeId, _winPatternInput.Text.Trim());
		}
		catch (Exception ex)
		{
			_outputText.Text = $"[color=red][b]ERROR[/b][/color]\n{ex.Message}\n\n{ex.StackTrace}";
		}
	}

	private string BuildReport(string archetypeId, string rawPattern)
	{
		var rng = new Random();
		var controller = NarrativeController.Create(rng);
		controller.StartCampaign(archetypeId);

		var aiNamingCfg = ConfigLoader.Load<AiNamingConfig>("res://config/ai_naming.json");
		var enemy = AiNaming.GenerateCharacter(aiNamingCfg, AiDisposition.Aggressive, rng);
		controller.UpdateEnemy(enemy);

		var state = controller.CurrentState;
		var chapters = state.TotalChapters;
		var wins = ParseWinPattern(rawPattern, chapters);

		var sb = new StringBuilder();

		AppendHeader(sb, $"CAMPAIGN: {ArchetypeDisplayName(state.ArchetypeId)}");
		AppendField(sb, "Archetype", state.ArchetypeId);
		AppendField(sb, "Faction", $"{state.Player.FactionName} ({state.Player.Title})");
		AppendField(sb, "Enemy", $"{state.Enemy.FactionName} ({state.Enemy.Title})");
		AppendField(sb, "Chapters", chapters.ToString());
		AppendField(sb, "Pattern", string.Join(", ", Array.ConvertAll(wins, w => w ? "W" : "L")));
		sb.AppendLine();

		for (var i = 0; i < chapters; i++)
		{
			var ctx = controller.GetNextMission();
			var won = wins[i];

			sb.AppendLine($"[color=cyan][b]── Chapter {i + 1}/{chapters}: {ctx.Chapter.ChapterTitle}  ({ctx.Chapter.ChapterId}) ──[/b][/color]");
			sb.AppendLine();

			if (ctx.InterludeNodeName != null)
			{
				var capturedCtx = ctx;
				var capturedLabel = $"Interlude {i + 1}";
				AddPreviewButton(capturedLabel, () => PreviewYarnNode(capturedCtx.InterludeNodeName!, BuildInterludeVars(capturedCtx)));

				AppendSubheader(sb, "INTERLUDE");
				AppendField(sb, "Yarn Node", ctx.InterludeNodeName, indent: true);
				AppendField(sb, "Sector", ctx.SectorName, indent: true);
				AppendField(sb, "Date", ctx.InterludeDate, indent: true);
				sb.AppendLine();
			}

			AppendSubheader(sb, "MISSION");
			AppendField(sb, "Condition", ctx.Condition.Id, indent: true);
			AppendField(sb, "Tags", $"[{string.Join(", ", ctx.Condition.Tags)}]", indent: true);
			AppendField(sb, "Objective", ctx.Condition.Description, indent: true);
			if (ctx.Condition.TimeoutSeconds.HasValue)
				AppendField(sb, "Timeout", $"{ctx.Condition.TimeoutSeconds}s — {ctx.Condition.TimeoutMessage}", indent: true);
			sb.AppendLine();

			AppendSubheader(sb, "BRIEFING");
			AppendIndented(sb, ctx.Briefing);
			sb.AppendLine();

			if (ctx.Chapter.IntroBarks.Length > 0)
			{
				AppendSubheader(sb, "INTRO BARKS");
				foreach (var bark in ctx.Chapter.IntroBarks)
					sb.AppendLine($"  • {bark}");
				sb.AppendLine();
			}

			AppendSubheader(sb, "NARRATIVE BARKS");
			var anyBark = false;
			foreach (BarkTrigger trigger in Enum.GetValues<BarkTrigger>())
			{
				var bark = controller.TryGetBark(trigger);
				if (bark == null) continue;
				sb.AppendLine($"  [{trigger}]  {bark}");
				anyBark = true;
			}
			if (!anyBark) sb.AppendLine("  (none match current state)");
			sb.AppendLine();

			var outcomeColor = won ? "lime" : "tomato";
			sb.AppendLine($"[color={outcomeColor}][b]▸ OUTCOME: {(won ? "WIN" : "LOSS")}[/b][/color]");
			if (won)
				sb.AppendLine($"  {ctx.Condition.EndDescription}");
			else
				sb.AppendLine($"  {ctx.Condition.TimeoutMessage ?? "Mission failed."}");
			sb.AppendLine();

			if (ctx.Chapter.OutroBarks.Length > 0)
			{
				AppendSubheader(sb, "OUTRO BARKS");
				foreach (var bark in ctx.Chapter.OutroBarks)
					sb.AppendLine($"  • {bark}");
				sb.AppendLine();
			}

			var playerSys = won ? 8 : 3;
			var enemySys  = won ? 2 : 7;
			controller.OnMissionComplete(won, playerSys, enemySys, enemySys / 2);
		}

		if (controller.IsCampaignComplete)
		{
			_outroNodeName = controller.GetOutroNodeName();
			_outroVars = controller.BuildOutroVars();
			_previewOutroButton.Disabled = false;

			AppendHeader(sb, "OUTRO");
			AppendField(sb, "Yarn Node", _outroNodeName);
			sb.AppendLine();

			var finalState = controller.CurrentState;
			sb.AppendLine($"  Final record:  [b]{finalState.MissionsWon} / {finalState.MissionsCompleted}[/b] wins");
		}

		return sb.ToString();
	}

	private static Dictionary<string, string> BuildInterludeVars(MissionContext ctx) => new()
	{
		["$player_faction"]    = ctx.State.Player.FactionName,
		["$enemy_faction"]     = ctx.State.Enemy.FactionName,
		["$sector_name"]       = ctx.SectorName,
		["$mission_objective"] = ctx.Condition.Description,
		["$chapter_title"]     = ctx.Chapter.ChapterTitle,
		["$date"]              = ctx.InterludeDate,
	};

	private void PreviewYarnNode(string nodeName, IReadOnlyDictionary<string, string> vars)
	{
		var panel = GD.Load<PackedScene>(NarrativePanelPath).Instantiate<NarrativePanel>();
		AddChild(panel);
		panel.ShowNarrative(nodeName, vars, onComplete: panel.QueueFree);
	}

	private void PreviewOutro()
	{
		if (_outroNodeName == null || _outroVars == null) return;
		var panel = GD.Load<PackedScene>(NarrativePanelPath).Instantiate<NarrativePanel>();
		AddChild(panel);
		panel.ShowNarrative(_outroNodeName, _outroVars, onComplete: panel.QueueFree);
	}

	private void AddPreviewButton(string label, Action callback)
	{
		var btn = new Button { Text = label };
		btn.Pressed += callback;
		_interludePreviewButtons.AddChild(btn);
	}

	// ── Formatting helpers ──

	private static void AppendHeader(StringBuilder sb, string text)
		=> sb.AppendLine($"[color=yellow][b]══ {text} ══[/b][/color]");

	private static void AppendSubheader(StringBuilder sb, string text)
		=> sb.AppendLine($"[color=green]▸ {text}[/color]");

	private static void AppendField(StringBuilder sb, string key, string value, bool indent = false)
	{
		var pad = indent ? "  " : "";
		sb.AppendLine($"{pad}  {key,-12} [b]{value}[/b]");
	}

	private static void AppendIndented(StringBuilder sb, string text)
	{
		foreach (var line in text.Split('\n'))
			sb.AppendLine($"  {line}");
	}

	private static bool[] ParseWinPattern(string input, int chapters)
		=> NarrativeCli.ParseWinPattern(input, chapters);

	private static string ArchetypeDisplayName(string id) => id switch
	{
		"falling_empire" => "The Falling Empire",
		"rising_power"   => "The Rising Power",
		"conquest"       => "Total Conquest",
		_                => id
	};
}
