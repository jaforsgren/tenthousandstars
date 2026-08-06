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
// commitment pre/post dialogue, story interludes, campaign outros, and map events.
//
// Dialogues are queued: only one runs at a time and any request made while one is
// showing (e.g. a map event firing mid-commitment dialogue) waits its turn.
//
// Scene layout expected (NarrativePanel.tscn):
//   NarrativePanel (CanvasLayer — this script)
//   └── DialogueContent (DialogueContent.tscn — DialogueController)
//       ├── YarnBridge
//       ├── DialogueRunnerNode
//       └── InMemoryVariableStorage
public partial class NarrativePanel : CanvasLayer
{
	private readonly record struct NarrativeRequest(string YarnNode, IReadOnlyDictionary<string, string> Vars, Action? OnComplete);

	private static readonly IReadOnlyDictionary<string, string> EmptyVars = new Dictionary<string, string>();

	private DialogueRunner _runner = null!;
	private InMemoryVariableStorage _vars = null!;
	private DialogueController _dialogueController = null!;
	private readonly Queue<NarrativeRequest> _pending = new();
	private bool _running;
	private Action? _onComplete;

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

	public void ShowNarrative(string yarnNode, IReadOnlyDictionary<string, string> vars, Action? onComplete = null)
		=> Enqueue(new NarrativeRequest(yarnNode, new Dictionary<string, string>(vars), onComplete));

	public void SetNarrativeVar(string name, string value) => _vars.SetValue(name, value);

	public void ShowPreCommitment(IntentType intent, Action onComplete)
		=> Enqueue(new NarrativeRequest(PreCommitYarnNode(intent), EmptyVars, onComplete));

	public void OnCommitmentResolved(int systemIndex, int ownerInt, int intentInt, bool controlGained, float remainingStrength)
	{
		if ((SystemOwner)ownerInt != SystemOwner.Player) return;

		var intent = (IntentType)intentInt;
		Enqueue(new NarrativeRequest(
			ResolveYarnNode(intent, controlGained),
			new Dictionary<string, string>
			{
				["$control_gained"]     = controlGained ? "true" : "false",
				["$remaining_strength"] = remainingStrength.ToString(CultureInfo.InvariantCulture),
				["$system_index"]       = systemIndex.ToString(CultureInfo.InvariantCulture)
			},
			null));
	}

	private void Enqueue(NarrativeRequest request)
	{
		_pending.Enqueue(request);
		TryRunNext();
	}

	private void TryRunNext()
	{
		if (_running || _pending.Count == 0) return;

		var request = _pending.Dequeue();
		_running = true;
		LastNarrativeAction = "";
		_onComplete = request.OnComplete;
		ApplyVars(request.Vars);
		GameSpeed.PushUiPause();
		Show();
		RunDialogueSafe(request.YarnNode);
	}

	private void ApplyVars(IReadOnlyDictionary<string, string> vars)
	{
		foreach (var (k, v) in vars)
		{
			// Yarn variables are typed; coerce boolean/numeric strings so they
			// don't silently fall back to their declared default.
			if (bool.TryParse(v, out var b))
				_vars.SetValue(k, b);
			else if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
				_vars.SetValue(k, f);
			else
				_vars.SetValue(k, v);
		}
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
		_running = false;
		Hide();
		GameSpeed.PopUiPause();
		var callback = _onComplete;
		_onComplete = null;
		callback?.Invoke();
		TryRunNext();
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
