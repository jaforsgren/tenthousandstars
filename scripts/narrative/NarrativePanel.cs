using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using YarnSpinnerGodot;
using Tts.Commitment;
using Tts.Dialogue;
using Tts.Level;

namespace Tts.Narrative;

// CanvasLayer that hosts the YarnSpinner dialogue runner for all in-game narrative:
// commitment pre/post dialogue, story interludes, and campaign outros.
//
// Scene layout expected (NarrativePanel.tscn):
//   NarrativePanel (CanvasLayer — this script)
//   └── DialogueContent (DialogueContent.tscn — DialogueController)
//       ├── YarnBridge
//       ├── DialogueRunnerNode
//       └── InMemoryVariableStorage
public partial class NarrativePanel : CanvasLayer
{
	private DialogueRunner _runner = null!;
	private InMemoryVariableStorage _vars = null!;
	private DialogueController _dialogueController = null!;
	private Action? _onDialogueComplete;

	public string LastNarrativeAction { get; private set; } = "";

	public override void _Ready()
	{
		_runner = GetNode<DialogueRunner>("DialogueContent/DialogueRunnerNode");
		_vars   = GetNode<InMemoryVariableStorage>("DialogueContent/InMemoryVariableStorage");
		_dialogueController = GetNode<DialogueController>("DialogueContent");

		var bridge = GetNode<YarnBridge>("DialogueContent/YarnBridge");
		bridge.dialogueRunner = _runner;
		_runner.dialoguePresenters.Add(bridge);

		_runner.onDialogueComplete += OnDialogueComplete;

		_runner.AddCommandHandler("narrative_action",
			new Action<string>(action => LastNarrativeAction = action));

		Hide();
	}

	public void ShowEncounterEvent(string yarnNode, Action onComplete)
	{
		_onDialogueComplete = onComplete;
		GameSpeed.PushUiPause();
		Show();
		RunDialogueSafe(yarnNode);
	}

	public void ShowNarrative(string yarnNode, IReadOnlyDictionary<string, string> vars, Action? onComplete = null)
	{
		LastNarrativeAction = "";
		foreach (var (k, v) in vars)
		{
			// Yarn variables are typed; coerce numeric strings to float so number
			// variables don't silently fall back to their declared default.
			if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
				_vars.SetValue(k, f);
			else
				_vars.SetValue(k, v);
		}
		_onDialogueComplete = onComplete;
		GameSpeed.PushUiPause();
		Show();
		RunDialogueSafe(yarnNode);
	}

	public void SetNarrativeVar(string name, string value) => _vars.SetValue(name, value);

	public void ShowPreCommitment(IntentType intent, Action onComplete)
	{
		_onDialogueComplete = onComplete;
		GameSpeed.PushUiPause();
		Show();
		RunDialogueSafe(PreCommitYarnNode(intent));
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
		RunDialogueSafe(ResolveYarnNode(intent, controlGained));
	}

	private async void RunDialogueSafe(string yarnNode)
	{
		_dialogueController.Clear();
		try
		{
			await _runner.StartDialogue(yarnNode);
		}
		catch (Exception ex)
		{
			GD.PushError($"[NarrativePanel] Dialogue error in '{yarnNode}': {ex.Message}");
			OnDialogueComplete();
		}
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
		_                      => "attack_won"
	};
}
