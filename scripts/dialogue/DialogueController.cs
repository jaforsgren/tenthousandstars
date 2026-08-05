#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
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

	// Animation timings come from config/ui.json (Dialogue section) so the
	// dialogue feel can be tuned without a rebuild.
	private float _historicAlpha;
	private float _historicFadeDuration;
	private float _scrollDuration;
	private float _sectionFadeOutDuration;
	private float _sectionCollapseDuration;

	[Export] private float SpeakerDelaySeconds = 2.0f;
	[Export] private float LinePauseSeconds = 1.0f;
	[Export] private string PreviewNode = "";

	private DialogueController() { }
	private DialogEntry? _currentEntry;
	private DialogEntry? _lastDialogEntry;
	private Control? _focusEntry;
	private float _scrollVelocity;
	private const float _maxScrollRate = 2400f;
	private string _lastSpeaker = "";
	private string _lastLineSpeaker = "";
	private string _lastNamedSpeaker = "";

	private readonly Queue<List<Node>> _completedSections = new();
	private List<Node> _currentSectionEntries = new();
	private CancellationTokenSource? _scrollCts;

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

		DialogueConfig dialogue = ConfigLoader.Load<UiConfig>("res://config/ui.json").Dialogue;
		_historicAlpha = dialogue.HistoricAlpha;
		_historicFadeDuration = dialogue.HistoricFadeDuration;
		_scrollDuration = dialogue.ScrollDuration;
		_sectionFadeOutDuration = dialogue.SectionFadeOutDuration;
		_sectionCollapseDuration = dialogue.SectionCollapseDuration;

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
		_focusEntry = null;
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
		_focusEntry = entry;
		DimPreviousEntries(entry);
		CallDeferred(nameof(ScrollToActive));

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

		CallDeferred(nameof(ScrollToActive));
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
		_focusEntry = entry;
		DimPreviousEntries(entry);

		await entry.ShowAsync(result);

		_bridge.Adapter.AcknowledgeSkillCheck();
		CallDeferred(nameof(ScrollToActive));
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
		_focusEntry = entry;
		DimPreviousEntries(entry);

		entry.Setup(options, id =>
		{
			FinalizeCurrentSection();
			_lastNamedSpeaker = "";
			_bridge.Adapter.SelectOption(id);
			CallDeferred(nameof(ScrollToActive));
		});

		CallDeferred(nameof(ScrollToActive));
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

	private async void FadeOutSection(List<Node> entries)
	{
		var tasks = new List<Task>();
		foreach (Node e in entries)
			if (IsInstanceValid(e) && e is CanvasItem canvas)
				tasks.Add(FadeAlphaAsync(canvas, 0f, _sectionFadeOutDuration));

		await Task.WhenAll(tasks);
		if (IsInstanceValid(this))
			CollapseAndFreeSection(entries);
	}

	private async void CollapseAndFreeSection(List<Node> entries)
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

		float duration = _sectionCollapseDuration;
		float startMs = Time.GetTicksMsec();
		while (IsInstanceValid(spacer))
		{
			float elapsed = (Time.GetTicksMsec() - startMs) / 1000f;
			if (elapsed >= duration) break;
			float height = Mathf.Lerp(totalHeight, 0f, Mathf.Clamp(elapsed / duration, 0f, 1f));
			spacer.CustomMinimumSize = new Vector2(0f, height);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		if (IsInstanceValid(spacer)) spacer.QueueFree();
	}

	// ── Scroll / layout ──────────────────────────────────────────────────────

	// Fades a canvas item's alpha to a target over a duration. Runs as a frame
	// loop driven by real ticks so it still animates while GameSpeed pauses the
	// engine (Engine.TimeScale = 0), which home Tweens freeze on.
	private async Task FadeAlphaAsync(CanvasItem target, float targetAlpha, float duration)
	{
		float startAlpha = target.Modulate.A;
		if (Mathf.IsEqualApprox(startAlpha, targetAlpha)) return;

		float startMs = Time.GetTicksMsec();
		while (IsInstanceValid(target))
		{
			float elapsed = (Time.GetTicksMsec() - startMs) / 1000f;
			if (elapsed >= duration) break;
			Color c = target.Modulate;
			target.Modulate = new Color(c.R, c.G, c.B, Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp(elapsed / duration, 0f, 1f)));
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		if (IsInstanceValid(target))
		{
			Color c = target.Modulate;
			target.Modulate = new Color(c.R, c.G, c.B, targetAlpha);
		}
	}

	// Dims every already-delivered entry in the stack (section headers, dialog,
	// skill checks, choices) so the freshly added active row is the only fully
	// focused text. Layout spacer Controls are opaque and therefore untouched.
	private void DimPreviousEntries(Control active)
	{
		if (!IsInstanceValid(_dialogStack)) return;
		foreach (Node child in _dialogStack.GetChildren())
		{
			if (child == active) continue;
			if (child is not (DialogEntry or SkillCheckEntry or ChoiceEntry or Label)) continue;
			_ = FadeAlphaAsync((CanvasItem)child, _historicAlpha, _historicFadeDuration);
		}
	}

	// The stack resizes whenever an entry is appended or a collapsed section is
	// removed. Re-centering on the focus at that exact moment guarantees the
	// text being written stays at the vertical middle of the viewport instead of
	// slipping past it, even when a manual scroll happened the frame before.
	private void OnDialogStackResized()
	{
		CallDeferred(nameof(ScrollToActive));
	}

	// Called via CallDeferred so Godot has flushed the layout pass before we
	// update the scroll position. Scrolls so the active row sits near the
	// vertical center of the viewport; ScrollContainer clamps the value to the
	// content bounds, so short dialogues simply sit at the top.
	private void ScrollToActive()
	{
		if (!IsInstanceValid(_scrollContainer)) return;

		_scrollCts?.Cancel();
		_scrollCts?.Dispose();
		_scrollCts = new CancellationTokenSource();
		_ = AnimateScrollToActiveAsync(_scrollCts.Token);
	}

	// Scrolls toward the active row's center target with a critically damped
	// spring. The target is recomputed every frame from the live layout
	// (positions are only valid once Godot has sorted the stack, which lags the
	// Resized event). Critically damped follow gives a continuous glide that
	// decelerates fully without overshoot, and settles exactly on the target —
	// no asymptotic crawl that snap-settles. The loop ends once both the scroll
	// value and velocity are negligible near the target (or the target is the
	// unclamped container bottom, which ScrollContainer pins down).
	private async Task AnimateScrollToActiveAsync(CancellationToken token)
	{
		float omega = Mathf.Pi * 2f / _scrollDuration;
		float lastMs = Time.GetTicksMsec();

		while (IsInstanceValid(_scrollContainer) && !token.IsCancellationRequested)
		{
			float now = Time.GetTicksMsec();
			float delta = Mathf.Max(0f, (now - lastMs) / 1000f);
			lastMs = now;

			int target = ComputeScrollTarget();
			float position = _scrollContainer.ScrollVertical;

			_scrollVelocity += (target - position) * omega * omega * delta;
			_scrollVelocity -= 2f * omega * _scrollVelocity * delta;
			position += _scrollVelocity * delta;

			// A fresh spring must start from the row below hot launch, so cap the
			// frame growth to avoid an overshooting first hop for long distances.
			_scrollVelocity = Mathf.Clamp(_scrollVelocity, -_maxScrollRate, _maxScrollRate);

			bool converged = Mathf.Abs(position - target) < 0.5f && Mathf.Abs(_scrollVelocity) < 0.5f;
			_scrollContainer.ScrollVertical = Mathf.RoundToInt(converged ? target : position);

			if (converged)
			{
				_scrollVelocity = 0f;
				break;
			}
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}

	private int ComputeScrollTarget()
	{
		// No focus yet — glide to the bottom of the stack.
		if (_focusEntry is null || !IsInstanceValid(_focusEntry))
		{
			ScrollBar bar = _scrollContainer.GetVScrollBar();
			return Mathf.Max(0, Mathf.RoundToInt(bar?.MaxValue ?? 0f));
		}

		float viewportHeight = Mathf.Max(1f, _scrollContainer.Size.Y);
		float centerOffset = _focusEntry.Position.Y + _focusEntry.Size.Y * 0.5f - viewportHeight * 0.5f;
		return Mathf.Max(0, Mathf.RoundToInt(centerOffset));
	}
}
