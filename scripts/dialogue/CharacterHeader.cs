#nullable enable

using System;
using System.Threading.Tasks;
using Godot;
using YarnSpinnerGodot;

namespace Tts.Dialogue;

/// <summary>
/// Displayed once at the top of each NPC speaker segment.
/// Shows portrait, speaker name, and current descriptor.
///
/// Expected scene structure (CharacterHeader.tscn):
///   HBoxContainer (this node)
///     Control       "PortraitWrapper"
///       Portraits   "PortraitWrapper/Portraits"
///       TextureRect "PortraitWrapper/PortraitFrame"
///     VBoxContainer "Info"
///       Label       "Info/SpeakerName"
///       Label       "Info/Descriptor"
/// </summary>
[Tool]
public partial class CharacterHeader : HBoxContainer
{
	private static readonly Color Transparent = new(1f, 1f, 1f, 0f);
	private static readonly Color Opaque = new(1f, 1f, 1f, 1f);
	private const float FadeInDuration = 0.4f;

	private Portraits _portraits = null!;
	private Label _speakerName = null!;
	private Label _descriptor = null!;
	private VBoxContainer _info = null!;

	public override void _Ready()
	{
		_portraits = GetNode<Portraits>("PortraitWrapper/Portraits");
		_speakerName = GetNode<Label>("Info/SpeakerName");
		_descriptor = GetNode<Label>("Info/Descriptor");
		_info = GetNode<VBoxContainer>("Info");

		_descriptor.ThemeTypeVariation = "Descriptor";
	}

	public string GetSpeaker() => _speakerName.Text;

	public void SetDescription(string descriptor)
	{
		_descriptor.Text = descriptor;
		_descriptor.Visible = !string.IsNullOrWhiteSpace(descriptor);
	}

	// Synchronous, no animation — used by editor tool preview.
	public void SetPreview(string speaker, string descriptor)
	{
		if (_speakerName is null) return;
		_speakerName.Text = speaker;
		_descriptor.Text = descriptor;
		_descriptor.Visible = !string.IsNullOrWhiteSpace(descriptor);
		_portraits.Visible = _portraits.ShowPortrait(speaker);
		Modulate = Opaque;
		Visible = true;
	}

	public void Reset()
	{
		_speakerName.Text = "";
		_descriptor.Text = "";
		_descriptor.Visible = false;
		_portraits.HideAll();
		Modulate = Opaque;
		Visible = false;
	}

	public async Task ShowAsync(string speaker, string descriptor)
	{
		_descriptor.Text = descriptor;
		_descriptor.Visible = !string.IsNullOrWhiteSpace(descriptor);

		if (_speakerName.Text == speaker)
			return;

		_speakerName.Text = speaker;
		_portraits.Visible = true;
		await _portraits.ShowPortraitAsync(speaker, FadeInDuration);

		Visible = true;
		_info.Modulate = Transparent;
		await FadeInAsync();
	}

	private async Task FadeInAsync()
	{
		ulong startMs = Time.GetTicksMsec();
		while (IsInstanceValid(this))
		{
			float elapsed = (Time.GetTicksMsec() - startMs) / 1000f;
			if (elapsed >= FadeInDuration) break;
			_info.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(elapsed / FadeInDuration, 0f, 1f));
			await YarnTask.NextFrame();
		}
		if (IsInstanceValid(this))
			_info.Modulate = Opaque;
	}
}
