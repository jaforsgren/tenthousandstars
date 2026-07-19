#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using YarnSpinnerGodot;

namespace Tts.Dialogue;

/// <summary>
/// A single stacked dialogue entry. Speaker styling is applied via BBCode:
///   "you"      → muted "| text" with "YOU" label shown
///   "narration" / "" → muted italic text, no label
///   other (NPC) → plain text from markup parser
///
/// Expected scene structure (DialogEntry.tscn):
///   VBoxContainer (this node)
///     Label         "YouLabel"
///     RichTextLabel "DialogueText"
///     TextureRect   "Divider"
/// </summary>
public partial class DialogEntry : VBoxContainer
{
	private static readonly Color Transparent = new(1f, 1f, 1f, 0f);
	private static readonly Color Opaque = new(1f, 1f, 1f, 1f);
	private const float FadeInDuration = 0.25f;

	private Label _youLabel = null!;
	private RichTextLabel _dialogueText = null!;
	// private Line2D _divider = null!;
	private TextureRect _divider = null!; 

	private CancellationTokenSource? _typewriterCts;

	public override void _Ready()
	{
		_youLabel = GetNode<Label>("YouLabel");
		_dialogueText = GetNode<RichTextLabel>("DialogueText");
		// _divider = GetNode<Line2D>("Divider");
		_divider = GetNode<TextureRect>("Divider2");
		_youLabel.ThemeTypeVariation = "YouLabel";
	}

	public async Task ShowAsync(YarnLine line)
	{
		int prefixLen = ApplyLineStyle(line);

		Modulate = Transparent;
		await FadeInAsync();

		if (!IsInstanceValid(this)) return;

		_typewriterCts?.Dispose();
		_typewriterCts = new CancellationTokenSource();

		await RunTypewriterAsync(line.ParsedText.CharDelays, prefixLen, _typewriterCts.Token);

		if (IsInstanceValid(this))
			_dialogueText.VisibleRatio = 1.0f;
	}

	public void SkipTypewriter() => _typewriterCts?.Cancel();

	public void HideDivider() => _divider.Visible = false;

	/// <summary>Returns the number of prefix characters that should animate instantly.</summary>
	private int ApplyLineStyle(YarnLine line)
	{
		_dialogueText.BbcodeEnabled = true;
		_dialogueText.VisibleCharacters = 0;

		switch (line.Speaker.ToLowerInvariant())
		{
			case "you":
				_youLabel.Visible = true;
				_dialogueText.Text = $"[color=#777777]| {line.ParsedText.BBCodeText}[/color]";
				_divider.Visible = true;
				return 2; // "| " prefix appears instantly

			case "narration":
			case "narrator":
			case "":
				_youLabel.Visible = false;
				_dialogueText.ThemeTypeVariation = "Narration";
				_dialogueText.Text = $"[i]{line.ParsedText.BBCodeText}[/i]";
				_divider.Visible = true;
				return 0;

			default:
				_youLabel.Visible = false;
				_dialogueText.Text = line.ParsedText.BBCodeText;
				return 0;
		}
	}

	private async Task FadeInAsync()
	{
		ulong startMs = Time.GetTicksMsec();
		while (IsInstanceValid(this))
		{
			float elapsed = (Time.GetTicksMsec() - startMs) / 1000f;
			if (elapsed >= FadeInDuration) break;
			Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(elapsed / FadeInDuration, 0f, 1f));
			await YarnTask.NextFrame();
		}
		if (IsInstanceValid(this))
			Modulate = Opaque;
	}

	private async Task RunTypewriterAsync(float[] charDelays, int prefixLen, CancellationToken token)
	{
		// GetParsedText gives visible character count excluding BBCode tags
		int count = _dialogueText.GetParsedText().Length;

		for (int i = 0; i < count; i++)
		{
			if (!IsInstanceValid(this) || token.IsCancellationRequested)
				break;

			int delayIndex = i - prefixLen;
			float delaySec = delayIndex < 0
				? 0f
				: (delayIndex < charDelays.Length ? charDelays[delayIndex] : 0.04f);

			if (delaySec > 0f)
				await YarnTask.Delay(TimeSpan.FromSeconds(delaySec), token).SuppressCancellationThrow();

			if (!IsInstanceValid(this))
				return;

			_dialogueText.VisibleCharacters = i + 1;
		}

		if (IsInstanceValid(this))
			_dialogueText.VisibleRatio = 1.0f;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_typewriterCts?.Dispose();
			_typewriterCts = null;
		}
		base.Dispose(disposing);
	}
}
