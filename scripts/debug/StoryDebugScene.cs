using System;
using System.Text;
using Godot;

namespace Tts;

public partial class StoryDebugScene : Control
{
    private OptionButton _archetypeOption = null!;
    private LineEdit _winPatternInput = null!;
    private Button _generateButton = null!;
    private Button _backButton = null!;
    private HBoxContainer _interludePreviewButtons = null!;
    private Button _previewOutroButton = null!;
    private RichTextLabel _outputText = null!;

    private string _outroTitle = "";
    private string _outroText = "";

    private static readonly string[] ArchetypeIds = ["falling_empire", "rising_power", "conquest"];
    private static readonly string[] ArchetypeLabels = ["Falling Empire", "Rising Power", "Conquest"];

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
        _previewOutroButton.Pressed += ShowOutroPreview;
        _previewOutroButton.Disabled = true;
    }

    private void OnGeneratePressed()
    {
        _outroTitle = "";
        _outroText = "";
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

        var state = controller.CurrentState;
        var chapters = state.TotalChapters;
        var wins = ParseWinPattern(rawPattern, chapters);

        var sb = new StringBuilder();

        // ── Campaign header ──
        AppendHeader(sb, $"CAMPAIGN: {GetArchetypeName(state.ArchetypeId)}");
        AppendField(sb, "Archetype", state.ArchetypeId);
        AppendField(sb, "Faction", state.PlayerFactionName);
        AppendField(sb, "Chapters", chapters.ToString());
        AppendField(sb, "Pattern", string.Join(", ", Array.ConvertAll(wins, w => w ? "W" : "L")));
        sb.AppendLine();

        for (var i = 0; i < chapters; i++)
        {
            controller.UpdateEnemyFaction("The Dominion");
            var ctx = controller.GetNextMission();
            var won = wins[i];

            // ── Chapter header ──
            sb.AppendLine($"[color=cyan][b]── Chapter {i + 1}/{chapters}: {ctx.Chapter.ChapterTitle}  ({ctx.Chapter.ChapterId}) ──[/b][/color]");
            sb.AppendLine();

            // ── Interlude ──
            if (ctx.Interlude != null)
            {
                var capturedText = ctx.Interlude.Text;
                var capturedLabel = $"Interlude {i + 1}";
                AddPreviewButton(capturedLabel, () => ShowInterludePreview(capturedText));

                AppendSubheader(sb, "INTERLUDE");
                AppendIndented(sb, ctx.Interlude.Text);
                sb.AppendLine();
            }

            // ── Mission ──
            AppendSubheader(sb, "MISSION");
            AppendField(sb, "Condition", ctx.Condition.Id, indent: true);
            AppendField(sb, "Tags", $"[{string.Join(", ", ctx.Condition.Tags)}]", indent: true);
            AppendField(sb, "Objective", ctx.Condition.Description, indent: true);
            if (ctx.Condition.TimeoutSeconds.HasValue)
                AppendField(sb, "Timeout", $"{ctx.Condition.TimeoutSeconds}s — {ctx.Condition.TimeoutMessage}", indent: true);
            sb.AppendLine();

            // ── Briefing ──
            AppendSubheader(sb, "BRIEFING");
            AppendIndented(sb, ctx.Briefing);
            sb.AppendLine();

            // ── Intro barks ──
            if (ctx.Chapter.IntroBarks.Length > 0)
            {
                AppendSubheader(sb, "INTRO BARKS");
                foreach (var bark in ctx.Chapter.IntroBarks)
                    sb.AppendLine($"  • {bark}");
                sb.AppendLine();
            }

            // ── Narrative barks (all triggers) ──
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

            // ── Outcome ──
            var outcomeColor = won ? "lime" : "tomato";
            sb.AppendLine($"[color={outcomeColor}][b]▸ OUTCOME: {(won ? "WIN" : "LOSS")}[/b][/color]");
            if (won)
                sb.AppendLine($"  {ctx.Condition.EndDescription}");
            else
                sb.AppendLine($"  {ctx.Condition.TimeoutMessage ?? "Mission failed."}");
            sb.AppendLine();

            // ── Outro barks ──
            if (ctx.Chapter.OutroBarks.Length > 0)
            {
                AppendSubheader(sb, "OUTRO BARKS");
                foreach (var bark in ctx.Chapter.OutroBarks)
                    sb.AppendLine($"  • {bark}");
                sb.AppendLine();
            }

            // Simulate system counts: winning player has more systems
            var playerSys = won ? 8 : 3;
            var enemySys  = won ? 2 : 7;
            controller.OnMissionComplete(won, playerSys, enemySys, enemySys / 2);
        }

        // ── Outro ──
        if (controller.IsCampaignComplete)
        {
            var (title, text) = controller.GenerateOutro();
            _outroTitle = title;
            _outroText = text;
            _previewOutroButton.Disabled = false;

            AppendHeader(sb, "OUTRO");
            sb.AppendLine($"  [b]{title}[/b]");
            AppendIndented(sb, text);
            sb.AppendLine();

            var finalState = controller.CurrentState;
            sb.AppendLine($"  Final record:  [b]{finalState.MissionsWon} / {finalState.MissionsCompleted}[/b] wins");
        }

        return sb.ToString();
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

    // ── Preview helpers ──

    private void AddPreviewButton(string label, Action callback)
    {
        var btn = new Button { Text = label };
        btn.Pressed += callback;
        _interludePreviewButtons.AddChild(btn);
    }

    private void ShowInterludePreview(string text)
    {
        var layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        var panel = GD.Load<PackedScene>("res://scenes/ui/InterludePanel.tscn").Instantiate<InterludePanel>();
        layer.AddChild(panel);
        panel.Show(text, GetViewport().GetVisibleRect().Size, () => layer.QueueFree());
    }

    private void ShowOutroPreview()
    {
        var layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        var panel = GD.Load<PackedScene>("res://scenes/ui/OutroPanel.tscn").Instantiate<OutroPanel>();
        layer.AddChild(panel);
        panel.Show(_outroTitle, _outroText, GetViewport().GetVisibleRect().Size);
        panel.NewCampaignPressed += () => layer.QueueFree();
        panel.RandomMissionsPressed += () => layer.QueueFree();
        panel.QuitPressed += () => layer.QueueFree();
    }

    // ── Input parsing ──

    private static bool[] ParseWinPattern(string input, int chapters)
    {
        var result = new bool[chapters];
        if (string.IsNullOrEmpty(input))
        {
            Array.Fill(result, true);
            return result;
        }

        var parts = input.ToUpperInvariant().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < chapters; i++)
        {
            var idx = Math.Min(i, parts.Length - 1);
            result[i] = parts[idx] != "L";
        }
        return result;
    }

    private static string GetArchetypeName(string id) => id switch
    {
        "falling_empire" => "The Falling Empire",
        "rising_power"   => "The Rising Power",
        "conquest"       => "Total Conquest",
        _                => id
    };
}
