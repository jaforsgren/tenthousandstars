using System.Collections.Generic;
using Godot;
using Tts.Dialogue;

namespace Tts.Debug;

// Standalone debug scene for previewing any narrative Yarn node directly.
// Set as main scene, or open via F6 in-game (debug builds only).
// Instantiates CommitmentDialoguePanel the same way GameController does.
public partial class NarrativeDebugRoot : Control
{
    private LineEdit _nodeInput = null!;
    private LineEdit _playerFactionInput = null!;
    private LineEdit _enemyFactionInput = null!;
    private LineEdit _sectorNameInput = null!;
    private LineEdit _dateInput = null!;
    private LineEdit _missionObjectiveInput = null!;
    private Label _statusLabel = null!;
    private Button _playButton = null!;
    private Button _backButton = null!;

    private const string DialoguePanelPath = "res://scenes/dialogue/CommitmentDialoguePanel.tscn";

    private static readonly string[] QuickNodes =
    [
        "conquest_begin", "conquest_mid", "conquest_end",
        "outro_conquest_win", "outro_conquest_loss",
        "falling_empire_begin", "falling_empire_mid", "falling_empire_end",
        "outro_falling_empire_win", "outro_falling_empire_loss",
        "rising_power_begin", "rising_power_mid", "rising_power_end",
        "outro_rising_power_win", "outro_rising_power_loss",
    ];

    public override void _Ready()
    {
        _nodeInput            = GetNode<LineEdit>("%NodeInput");
        _playerFactionInput   = GetNode<LineEdit>("%PlayerFactionInput");
        _enemyFactionInput    = GetNode<LineEdit>("%EnemyFactionInput");
        _sectorNameInput      = GetNode<LineEdit>("%SectorNameInput");
        _dateInput            = GetNode<LineEdit>("%DateInput");
        _missionObjectiveInput = GetNode<LineEdit>("%MissionObjectiveInput");
        _statusLabel          = GetNode<Label>("%StatusLabel");
        _playButton           = GetNode<Button>("%PlayButton");
        _backButton           = GetNode<Button>("%BackButton");

        _playerFactionInput.Text   = "House Eternal";
        _enemyFactionInput.Text    = "Thaloran Alliance";
        _sectorNameInput.Text      = "The Veiled Span";
        _dateInput.Text            = "Sequence 1-2847";
        _missionObjectiveInput.Text = "Capture the marked system.";
        _nodeInput.Text            = "conquest_begin";

        _playButton.Pressed += OnPlayPressed;
        _backButton.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/Level.tscn");

        var quickButtons = GetNode<HFlowContainer>("%QuickNodeButtons");
        foreach (var node in QuickNodes)
        {
            var btn = new Button { Text = node };
            btn.Pressed += () => { _nodeInput.Text = btn.Text; OnPlayPressed(); };
            quickButtons.AddChild(btn);
        }
    }

    private void OnPlayPressed()
    {
        var nodeName = _nodeInput.Text.Trim();
        if (string.IsNullOrEmpty(nodeName))
        {
            SetStatus("Node name is empty.", error: true);
            return;
        }

        var vars = new Dictionary<string, string>
        {
            ["$player_faction"]    = _playerFactionInput.Text,
            ["$enemy_faction"]     = _enemyFactionInput.Text,
            ["$sector_name"]       = _sectorNameInput.Text,
            ["$date"]              = _dateInput.Text,
            ["$mission_objective"] = _missionObjectiveInput.Text,
            ["$missions_won"]      = "2",
            ["$total_missions"]    = "3",
        };

        var panelScene = GD.Load<PackedScene>(DialoguePanelPath);
        if (panelScene == null)
        {
            SetStatus($"Could not load {DialoguePanelPath}", error: true);
            return;
        }

        var panel = panelScene.Instantiate<CommitmentDialogueController>();
        AddChild(panel);
        SetStatus($"Playing: {nodeName}");
        panel.ShowNarrative(nodeName, vars, onComplete: () =>
        {
            panel.QueueFree();
            SetStatus($"Done: {nodeName}");
        });
    }

    private void SetStatus(string message, bool error = false)
    {
        _statusLabel.Text = message;
        _statusLabel.Modulate = error
            ? new Color(1f, 0.3f, 0.3f)
            : new Color(0.6f, 0.6f, 0.6f);
    }
}
