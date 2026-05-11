using System;
using Godot;
using YarnSpinnerGodot;

namespace Tts;

// Bridges CommitmentController resolution events to the YarnSpinner dialogue panel.
// Lives as a CanvasLayer that overlays the game viewport; auto-hides when dialogue ends.
//
// Scene layout expected (CommitmentDialoguePanel.tscn):
//   CommitmentDialoguePanel (CanvasLayer — this script)
//   ├── DialogueRoot (Control — DialogueController)
//   │   └── YarnBridge
//   ├── DialogueRunnerNode
//   ├── InMemoryVariableStorage
//   └── TextLineProvider
public partial class CommitmentDialogueController : CanvasLayer
{
    private DialogueRunner _runner = null!;
    private InMemoryVariableStorage _vars = null!;
    private Action? _onDialogueComplete;

    public override void _Ready()
    {
        _runner = GetNode<DialogueRunner>("DialogueRunnerNode");
        _vars   = GetNode<InMemoryVariableStorage>("InMemoryVariableStorage");

        var bridge = GetNode<YarnBridge>("DialogueRoot/YarnBridge");
        bridge.dialogueRunner = _runner;
        _runner.dialoguePresenters.Add(bridge);

        _runner.onDialogueComplete += OnDialogueComplete;

        Hide();
    }

    public void ShowPreCommitment(IntentType intent, Action onComplete)
    {
        _onDialogueComplete = onComplete;
        GameSpeed.PushUiPause();
        Show();
        _ = _runner.StartDialogue(PreCommitYarnNode(intent));
    }

    public void OnCommitmentResolved(int systemIndex, int ownerInt, int intentInt, bool controlGained, float remainingStrength)
    {
        if ((SystemOwner)ownerInt != SystemOwner.Player) return;

        var intent = (IntentType)intentInt;
        _vars.SetValue("$control_gained", controlGained);
        _vars.SetValue("$remaining_strength", remainingStrength);
        _vars.SetValue("$system_index", (float)systemIndex);

        _onDialogueComplete = null;
        GameSpeed.PushUiPause();
        Show();
        _ = _runner.StartDialogue(ResolveYarnNode(intent, controlGained));
    }

    private void OnDialogueComplete()
    {
        Hide();
        GameSpeed.PopUiPause();
        var callback = _onDialogueComplete;
        _onDialogueComplete = null;
        callback?.Invoke();
    }

    private static string PreCommitYarnNode(IntentType intent) => intent switch
    {
        IntentType.Attack      => "attack_commit",
        IntentType.Contest     => "contest_commit",
        IntentType.Fortify     => "fortify_commit",
        IntentType.Investigate => "investigate_commit",
        IntentType.Exploit     => "exploit_commit",
        _                      => "attack_commit"
    };

    private static string ResolveYarnNode(IntentType intent, bool controlGained) => intent switch
    {
        IntentType.Attack      => controlGained ? "attack_won"           : "attack_repulsed",
        IntentType.Contest     => controlGained ? "contest_destabilized" : "contest_failed",
        IntentType.Fortify     => "fortify_complete",
        IntentType.Investigate => controlGained ? "investigate_reveals"  : "investigate_quiet",
        IntentType.Exploit     => controlGained ? "exploit_extracted"    : "exploit_disrupted",
        _                      => "attack_done"
    };
}
