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
///   PinnedHeader/CharacterHeader – fixed node at the top; updated per NPC speaker
///   ScrollContainer / DialogStack – scrolling stack of dialogue entries
///
/// The CharacterHeader never scrolls. Dialogue entries auto-scroll downward; old
/// entries are pushed off the top through a gradient fade (TopFade shader node).
///
/// "you" and "narration" speakers have no portrait and never show a header.
/// NPC speaker changes update the pinned header in place with a fade.
///
/// A "section" is a consecutive run of lines from the same speaker, plus any
/// skill-check entries that appear inline during that run. Choice entries close
/// the active section. Only MaxSections sections are kept; the oldest fades out
/// when a new one begins.
///
/// Editor preview: set PreviewSpeaker / PreviewDescriptor / PreviewLines in the
/// inspector to populate the UI without running the game. All preview content is
/// cleared automatically when the game starts.
/// </summary>
[Tool]
public partial class DialogueController : Node
{
	[Export] private PackedScene _dialogEntryScene = null!;
	[Export] private PackedScene _skillCheckEntryScene = null!;
	[Export] private PackedScene _choiceEntryScene = null!;

	// ── Editor preview ───────────────────────────────────────────────────────

	private string _previewSpeaker = "";
	private string _previewDescriptor = "";
	private string[] _previewLines = Array.Empty<string>();

	[Export]
	private string PreviewSpeaker
	{
		get => _previewSpeaker;
		set { _previewSpeaker = value; if (Engine.IsEditorHint()) ApplyEditorPreview(); }
	}

	[Export]
	private string PreviewDescriptor
	{
		get => _previewDescriptor;
		set { _previewDescriptor = value; if (Engine.IsEditorHint()) ApplyEditorPreview(); }
	}

	[Export]
	private string[] PreviewLines
	{
		get => _previewLines;
		set { _previewLines = value; if (Engine.IsEditorHint()) ApplyEditorPreview(); }
	}

	// ── Runtime fields ───────────────────────────────────────────────────────

	private VBoxContainer _dialogStack = null!;
	private ScrollContainer _scrollContainer = null!;
	private Control _pinnedHeader = null!;
	private CharacterHeader _characterHeader = null!;
	private YarnBridge _bridge = null!;

	private const int MaxSections = 2;
	private const int MaxEntriesPerSection = 5;
	[Export] private float SpeakerDelaySeconds = 2.0f;
	[Export] private float LinePauseSeconds = 1.0f;
	[Export] private string PreviewNode = "";

	private DialogEntry? _currentEntry;
	private DialogEntry? _lastDialogEntry;
	private string _lastSpeaker = "";
	private string _lastLineSpeaker = "";
	private string _lastNamedSpeaker = "";

	private readonly Queue<List<Node>> _completedSections = new();
	private List<Node> _currentSectionEntries = new();

	private static readonly HashSet<string> PortraitlessSpeakers =
		new(StringComparer.OrdinalIgnoreCase) { "you", "narration", "narrator", "" };

	private const string PreviewMeta = "editor_preview";

	// ── Lifecycle ────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		_dialogStack     = GetNode<VBoxContainer>("ScrollContainer/MarginContainer/DialogStack");
		_scrollContainer = GetNode<ScrollContainer>("ScrollContainer");
		_pinnedHeader    = GetNode<Control>("PinnedHeader");
		_characterHeader = GetNode<CharacterHeader>("PinnedHeader/CharacterHeader");
		_dialogStack.Resized += OnDialogStackResized;

		if (Engine.IsEditorHint())
		{
			ApplyEditorPreview();
			return;
		}

		_bridge = GetNode<YarnBridge>("YarnBridge");

		// Game start: clear any editor preview content and start clean.
		_characterHeader.Reset();
		foreach (Node child in _dialogStack.GetChildren())
			child.QueueFree();

		if (!string.IsNullOrEmpty(PreviewNode) && HasNode("DialogueRunnerNode"))
		{
			var runner = GetNode<YarnSpinnerGodot.DialogueRunner>("DialogueRunnerNode");
			runner.dialoguePresenters.Add(_bridge);
			_ = runner.StartDialogue(PreviewNode);
		}

		_bridge.Adapter.LineReady += line =>
			OnLineReadyAsync(line).ContinueWith(t =>
				GD.PushError($"[DialogueController] Line render error: {t.Exception}"),
				TaskContinuationOptions.OnlyOnFaulted);
		_bridge.Adapter.SkillCheckReady += result =>
			OnSkillCheckReadyAsync(result).ContinueWith(t =>
				GD.PushError($"[DialogueController] Skill check render error: {t.Exception}"),
				TaskContinuationOptions.OnlyOnFaulted);
		_bridge.Adapter.OptionsReady += OnOptionsReady;
	}

	// ── Editor preview ───────────────────────────────────────────────────────

	private void ApplyEditorPreview()
	{
		if (_characterHeader is null) return;

		if (string.IsNullOrEmpty(_previewSpeaker))
			_characterHeader.Reset();
		else
			_characterHeader.SetPreview(_previewSpeaker, _previewDescriptor);

		if (_dialogStack is null) return;

		foreach (Node child in _dialogStack.GetChildren())
			if (child.HasMeta(PreviewMeta))
				child.QueueFree();

		foreach (string line in _previewLines)
		{
			if (string.IsNullOrWhiteSpace(line)) continue;
			var label = new RichTextLabel();
			label.BbcodeEnabled = true;
			label.FitContent = true;
			label.ScrollActive = false;
			label.SizeFlagsHorizontal = Control.SizeFlags.Fill;
			label.Text = line;
			label.SetMeta(PreviewMeta, true);
			_dialogStack.AddChild(label);
		}
	}

	// ── Public API ───────────────────────────────────────────────────────────

	public void Clear()
	{
		_completedSections.Clear();
		_currentSectionEntries.Clear();
		_currentEntry = null;
		_lastDialogEntry = null;
		_lastSpeaker = "";
		_lastLineSpeaker = "";
		_lastNamedSpeaker = "";
		_characterHeader.Reset();
		foreach (Node child in _dialogStack.GetChildren())
			child.QueueFree();
	}

	public override void _Input(InputEvent @event)
	{
		if (Engine.IsEditorHint()) return;
		if (!@event.IsActionPressed("ui_accept") || _currentEntry is null) return;

		_currentEntry.SkipTypewriter();
		GetViewport().SetInputAsHandled();
	}

	// ── Line rendering ───────────────────────────────────────────────────────

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
			await _characterHeader.ShowAsync(line.Speaker, line.Descriptor);

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
		CallDeferred(nameof(ScrollToBottom));

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

		CallDeferred(nameof(ScrollToBottom));
		_bridge.Adapter.AcknowledgeLine();
	}

	// ── Skill check rendering ────────────────────────────────────────────────

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
		if (_currentSectionEntries.Count == 0) return;

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

	// The stack resizes whenever an entry is appended or a collapsed section is
	// removed. Pinning the scroll to the bottom at that exact moment guarantees
	// the newest text never sits past the viewport, even when a manual scroll
	// happened a frame before layout settled.
	private void OnDialogStackResized()
	{
		CallDeferred(nameof(ScrollToBottom));
	}

	// Called via CallDeferred so Godot has flushed the layout pass before we
	// update the scroll position. int.MaxValue is clamped by ScrollContainer
	// to the actual content bottom — no need to read MaxValue from the scrollbar.
	private void ScrollToBottom()
	{
		if (!IsInstanceValid(_scrollContainer)) return;
		_scrollContainer.ScrollVertical = int.MaxValue;
	}
}
