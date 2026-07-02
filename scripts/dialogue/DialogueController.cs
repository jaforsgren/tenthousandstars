#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Tts.Dialogue;

/// <summary>
/// Root controller for the Disco-Elysium-style stacked dialogue UI.
///
/// Layout contract (DialogueContent.tscn):
///   PinnedHeader  – fixed Control at the top; holds the current CharacterHeader
///   ScrollContainer / DialogStack – scrolling stack of dialogue entries
///
/// The current NPC's CharacterHeader is pinned and never scrolls.
/// Dialogue entries auto-scroll downward; old entries are pushed off the top
/// through a gradient fade (TopFade shader node in the scene).
///
/// "you" and "narration" speakers have no portrait and never show a header.
/// NPC speaker changes replace the pinned header.
///
/// A "section" is a consecutive run of lines from the same speaker, plus any
/// skill-check entries that appear inline during that run. Choice entries close
/// the active section. Only MaxSections sections are kept; the oldest fades out
/// when a new one begins.
/// </summary>
public partial class DialogueController : Node
{
	[Export] private PackedScene _dialogEntryScene = null!;
	[Export] private PackedScene _skillCheckEntryScene = null!;
	[Export] private PackedScene _characterHeaderScene = null!;
	[Export] private PackedScene _choiceEntryScene = null!;

	private VBoxContainer _dialogStack = null!;
	private ScrollContainer _scrollContainer = null!;
	private Control _pinnedHeader = null!;
	private YarnBridge _bridge = null!;

	private const int MaxSections = 2;
	private const int MaxEntriesPerSection = 5;
	[Export] private float SpeakerDelaySeconds = 2.0f;
	[Export] private float LinePauseSeconds = 1.0f;
	[Export] private string PreviewNode = "";

	private DialogEntry? _currentEntry;
	private DialogEntry? _lastDialogEntry;
	private CharacterHeader? _activeHeader;
	private string _lastSpeaker = "";
	private string _lastLineSpeaker = "";
	private string _lastNamedSpeaker = "";

	private readonly Queue<List<Node>> _completedSections = new();
	private List<Node> _currentSectionEntries = new();
	private Tween? _scrollTween;

	private static readonly HashSet<string> PortraitlessSpeakers =
		new(StringComparer.OrdinalIgnoreCase) { "you", "narration", "narrator", "" };

	public override void _Ready()
	{
		_dialogStack = GetNode<VBoxContainer>("ScrollContainer/DialogStack");
		_scrollContainer = GetNode<ScrollContainer>("ScrollContainer");
		_pinnedHeader = GetNode<Control>("PinnedHeader");
		_bridge = GetNode<YarnBridge>("YarnBridge");

		foreach (Node child in _pinnedHeader.GetChildren())
			child.QueueFree();
		foreach (Node child in _dialogStack.GetChildren())
			child.QueueFree();

		if (!string.IsNullOrEmpty(PreviewNode) && HasNode("DialogueRunnerNode"))
		{
			var runner = GetNode<YarnSpinnerGodot.DialogueRunner>("DialogueRunnerNode");
			runner.dialoguePresenters.Add(_bridge);
			_ = runner.StartDialogue(PreviewNode);
		}

		_bridge.Adapter.LineReady += OnLineReadyTrigger;
		_bridge.Adapter.SkillCheckReady += OnSkillCheckReadyTrigger;
		_bridge.Adapter.OptionsReady += OnOptionsReady;
	}

	public override void _Input(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_accept") || _currentEntry is null)
			return;

		_currentEntry.SkipTypewriter();
		GetViewport().SetInputAsHandled();
	}

	// ── Line rendering ───────────────────────────────────────────────────────

	private void OnLineReadyTrigger(YarnLine line)
	{
		OnLineReadyAsync(line).ContinueWith(t =>
		{
			if (t.IsFaulted)
				GD.PushError($"[DialogueController] Line render error: {t.Exception}");
		}, TaskContinuationOptions.OnlyOnFaulted);
	}

	private async Task OnLineReadyAsync(YarnLine line)
	{
		if (_dialogEntryScene == null)
		{
			GD.PushError("[DialogueController] _dialogEntryScene is not assigned.");
			_bridge.Adapter.AcknowledgeLine();
			return;
		}

		bool isPortraitless = PortraitlessSpeakers.Contains(line.Speaker);

		if (!isPortraitless && line.Speaker != _lastSpeaker)
			await SwapCharacterHeaderAsync(line.Speaker, line.Descriptor);

		_lastSpeaker = isPortraitless ? "" : line.Speaker;

		if (!line.Speaker.Equals(_lastLineSpeaker, StringComparison.OrdinalIgnoreCase))
		{
			_lastLineSpeaker = line.Speaker;

			if (!isPortraitless && !line.Speaker.Equals(_lastNamedSpeaker, StringComparison.OrdinalIgnoreCase))
			{
				FinalizeCurrentSection();
				_lastNamedSpeaker = line.Speaker;
				if (SpeakerDelaySeconds > 0f)
					await ToSignal(GetTree().CreateTimer(SpeakerDelaySeconds, ignoreTimeScale: true), SceneTreeTimer.SignalName.Timeout);
				AddSectionHeader(line.Speaker);
			}
		}
		else
		{
			_lastDialogEntry?.HideDivider();
		}

		DialogEntry entry = _dialogEntryScene.Instantiate<DialogEntry>();
		_dialogStack.AddChild(entry);
		_currentSectionEntries.Add(entry);
		_currentEntry = entry;

		await entry.ShowAsync(line);

		_currentEntry = null;
		_lastDialogEntry = entry;

		if (_currentSectionEntries.Count >= MaxEntriesPerSection)
		{
			FinalizeCurrentSection();
			_lastLineSpeaker = "";
			_lastNamedSpeaker = "";
		}

		if (LinePauseSeconds > 0f)
			await ToSignal(GetTree().CreateTimer(LinePauseSeconds, ignoreTimeScale: true), SceneTreeTimer.SignalName.Timeout);

		_bridge.Adapter.AcknowledgeLine();
		CallDeferred(nameof(ScrollToBottom));
	}

	/// <summary>
	/// Replaces the pinned CharacterHeader with a new one for the given speaker.
	/// The header lives in PinnedHeader (outside the scroll stack) and never scrolls.
	/// </summary>
	private async Task SwapCharacterHeaderAsync(string speaker, string descriptor)
	{
		if (_characterHeaderScene == null)
		{
			GD.PushError("[DialogueController] _characterHeaderScene is not assigned.");
			return;
		}

		
		if (_activeHeader != null)
		{
			if (_activeHeader.GetSpeaker() == speaker)
			{
				_activeHeader.SetDescription(descriptor);
				return;
			}
		}

		// Fade out the outgoing header while the incoming one fades in (cross-fade).
		CharacterHeader? outgoing = _activeHeader;
		if (outgoing != null)
		{
			Tween fadeOut = CreateTween();
			fadeOut.TweenProperty(outgoing, "modulate:a", 0f, 0.4f)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.Out);
		}

		CharacterHeader header = _characterHeaderScene.Instantiate<CharacterHeader>();
		_pinnedHeader.AddChild(header);
		header.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_activeHeader = header;

		await header.ShowAsync(speaker, descriptor);

		// Outgoing is fully transparent by now; remove it cleanly.
		if (IsInstanceValid(outgoing))
			outgoing.QueueFree();
	}

	// ── Skill check rendering ────────────────────────────────────────────────

	private void OnSkillCheckReadyTrigger(SkillCheckResult result)
	{
		OnSkillCheckReadyAsync(result).ContinueWith(t =>
		{
			if (t.IsFaulted)
				GD.PushError($"[DialogueController] Skill check render error: {t.Exception}");
		}, TaskContinuationOptions.OnlyOnFaulted);
	}

	private async Task OnSkillCheckReadyAsync(SkillCheckResult result)
	{
		if (_skillCheckEntryScene == null)
		{
			GD.PushError("[DialogueController] _skillCheckEntryScene is not assigned.");
			_bridge.Adapter.AcknowledgeSkillCheck();
			return;
		}

		SkillCheckEntry entry = _skillCheckEntryScene.Instantiate<SkillCheckEntry>();
		_dialogStack.AddChild(entry);
		_currentSectionEntries.Add(entry);

		await entry.ShowAsync(result);

		_bridge.Adapter.AcknowledgeSkillCheck();
		CallDeferred(nameof(ScrollToBottom));
	}

	// ── Choices ──────────────────────────────────────────────────────────────

	private void OnOptionsReady(YarnOption[] options)
	{
		if (_choiceEntryScene == null)
		{
			GD.PushError("[DialogueController] _choiceEntryScene is not assigned.");
			return;
		}

		// Close the active speaker section before the choice block.
		FinalizeCurrentSection();

		_lastSpeaker = "";
		_lastLineSpeaker = "";
		_lastNamedSpeaker = "";
		_lastDialogEntry = null;

		ChoiceEntry entry = _choiceEntryScene.Instantiate<ChoiceEntry>();
		_dialogStack.AddChild(entry);
		_currentSectionEntries.Add(entry);

		entry.Setup(options, id =>
		{
			// Choice made: close the choice section so the next speaker opens fresh.
			FinalizeCurrentSection();
			_lastNamedSpeaker = "";
			_bridge.Adapter.SelectOption(id);
			CallDeferred(nameof(ScrollToBottom));
		});

		CallDeferred(nameof(ScrollToBottom));
	}

	// ── Section management ───────────────────────────────────────────────────

	private void AddSectionHeader(string speaker)
	{
		var label = new Label();
		label.Text = speaker.ToUpperInvariant();
		label.ThemeTypeVariation = "SectionHeader";
		_dialogStack.AddChild(label);
		_currentSectionEntries.Add(label);
	}

	private void FinalizeCurrentSection()
	{
		if (_currentSectionEntries.Count == 0)
			return;

		_completedSections.Enqueue(_currentSectionEntries);
		_currentSectionEntries = new List<Node>();

		while (_completedSections.Count > MaxSections - 1)
			FadeOutSection(_completedSections.Dequeue());
	}

	private void FadeOutSection(List<Node> entries)
	{
		Tween fadeTween = CreateTween();
		fadeTween.SetParallel(true);
		foreach (Node e in entries)
			if (IsInstanceValid(e))
				fadeTween.TweenProperty(e, "modulate:a", 0f, 2.0f);

		fadeTween.Finished += () => CollapseAndFreeSection(entries);
	}

	private void CollapseAndFreeSection(List<Node> entries)
	{
		// Measure the section's total height (including inter-node separation)
		// while nodes are still in the tree but invisible.
		int separation = _dialogStack.GetThemeConstant("separation");
		float totalHeight = 0f;
		int insertIndex = int.MaxValue;
		int validCount = 0;

		foreach (Node e in entries)
		{
			if (!IsInstanceValid(e)) continue;
			insertIndex = Mathf.Min(insertIndex, e.GetIndex());
			if (e is Control c)
			{
				totalHeight += c.Size.Y;
				validCount++;
			}
		}
		if (validCount > 1)
			totalHeight += (validCount - 1) * separation;

		foreach (Node e in entries)
			if (IsInstanceValid(e)) e.QueueFree();

		if (totalHeight <= 0f || insertIndex == int.MaxValue) return;

		// A spacer holds the visual gap while the collapse animation runs.
		// QueueFree is deferred, so the spacer is moved into place before nodes disappear.
		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(0f, totalHeight);
		_dialogStack.AddChild(spacer);
		_dialogStack.MoveChild(spacer, insertIndex);

		Tween collapseTween = CreateTween();
		collapseTween.TweenProperty(spacer, "custom_minimum_size:y", 0f, 0.5f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);
		collapseTween.Finished += () => { if (IsInstanceValid(spacer)) spacer.QueueFree(); };
	}

	// ── Scroll / layout ──────────────────────────────────────────────────────

	// Called via CallDeferred so layout has been flushed before we read MaxValue.
	// Killing the previous tween prevents competing animations from popping.
	private async void ScrollToBottom()
	{
		if (!IsInstanceValid(_scrollContainer)) return;

		// Wait one more frame so Godot finishes resizing the VBoxContainer.
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (!IsInstanceValid(_scrollContainer)) return;

		_scrollTween?.Kill();
		double target = _scrollContainer.GetVScrollBar().MaxValue;
		_scrollTween = CreateTween();
		_scrollTween.TweenProperty(_scrollContainer, "scroll_vertical", target, 10.4f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);
	}
}
