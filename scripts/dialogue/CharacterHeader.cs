#nullable enable

using System;
using System.Threading.Tasks;
using Godot;
using YarnSpinnerGodot;

namespace Tts;

/// <summary>
/// Displayed once at the top of each NPC speaker segment.
/// Shows portrait, speaker name, and current descriptor.
///
/// Expected scene structure (CharacterHeader.tscn):
///   HBoxContainer (this node)
///     TextureRect   "Portrait"
///     VBoxContainer "Info"
///       Label       "Info/SpeakerName"
///       Label       "Info/Descriptor"
/// </summary>
public partial class CharacterHeader : HBoxContainer
{
	private static readonly Color Transparent = new(1f, 1f, 1f, 0f);
	private static readonly Color Opaque = new(1f, 1f, 1f, 1f);
	private const float FadeInDuration = 0.4f;
	private const string PortraitBasePath = "res://portraits/";

	private TextureRect _portrait = null!;
	private Label _speakerName = null!;
	private Label _descriptor = null!;

	public override void _Ready()
	{
		_portrait = GetNode<TextureRect>("Portrait");
		_speakerName = GetNode<Label>("Info/SpeakerName");
		_descriptor = GetNode<Label>("Info/Descriptor");

		_descriptor.ThemeTypeVariation = "Descriptor";
	}
	
	
	public String GetSpeaker()
	{
		return _speakerName.Text;
	}
	
	public void SetDescription(string descriptor)
	{
		_descriptor.Text = descriptor;
		_descriptor.Visible = !string.IsNullOrWhiteSpace(descriptor);
	}

	public async Task ShowAsync(string speaker, string descriptor)
	{
		_descriptor.Text = descriptor;
		_descriptor.Visible = !string.IsNullOrWhiteSpace(descriptor);

		if (_speakerName.Text == speaker)
		{
			return;
		}

		_speakerName.Text = speaker;

		LoadPortrait(speaker);

		Modulate = Transparent;
		await FadeInAsync();
	}

	private void LoadPortrait(string speaker)
	{
		string path = $"{PortraitBasePath}{speaker.ToLowerInvariant()}.png";
		if (!ResourceLoader.Exists(path))
		{
			_portrait.Visible = false;
			return;
		}
		_portrait.Texture = ResourceLoader.Load<Texture2D>(path);
		_portrait.Visible = true;
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
}
