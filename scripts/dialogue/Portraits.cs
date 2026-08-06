#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Tts.Dialogue;

[Tool]
public partial class Portraits : Control
{
	private const string BackgroundLayerName = "portrait_bg";

	public bool ShowPortrait(string speakerName)
	{
		bool found = false;
		foreach (Node child in GetChildren())
		{
			if (child is not TextureRect portrait) continue;
			if (portrait.Name.ToString() == BackgroundLayerName)
			{
				portrait.Visible = true;
				continue;
			}
			bool match = portrait.Name.ToString().Equals(speakerName, StringComparison.OrdinalIgnoreCase);
			portrait.Visible = match;
			if (match) found = true;
		}
		return found;
	}

	// Fades the new portrait in on top of the old one. The old layer stays fully
	// visible until the new one reaches 100% opacity, then it is switched off with
	// no fade of its own.
	public async Task<bool> ShowPortraitAsync(string speakerName, float duration)
	{
		TextureRect? target = null;
		TextureRect? current = null;

		foreach (Node child in GetChildren())
		{
			if (child is not TextureRect portrait) continue;
			if (portrait.Name.ToString() == BackgroundLayerName)
			{
				portrait.Visible = true;
				continue;
			}
			if (portrait.Name.ToString().Equals(speakerName, StringComparison.OrdinalIgnoreCase))
				target = portrait;
			else if (portrait.Visible)
				current = portrait;
		}

		if (target is null)
		{
			if (current is not null) current.Visible = false;
			return false;
		}

		if (target == current) return true;

		// Draw the new layer on top of the current one: later siblings render
		// above earlier ones, while PortraitBg stays pinned at index 0.
		MoveChild(target, GetChildCount() - 1);
		target.Visible = true;
		target.Modulate = new Color(1f, 1f, 1f, 0f);
		await FadeAlphaAsync(target, 1f, duration);

		if (current is not null && IsInstanceValid(current))
			current.Visible = false;
		return true;
	}

	public void ShowRandomPortrait()
	{
		var faces = GetChildren().OfType<TextureRect>()
			.Where(portrait => portrait.Name.ToString() != BackgroundLayerName)
			.ToArray();
		if (faces.Length == 0) return;

		HideAll();
		faces[GD.RandRange(0, faces.Length - 1)].Visible = true;
	}

	public void HideAll()
	{
		foreach (Node child in GetChildren())
			if (child is TextureRect portrait)
				portrait.Visible = false;
	}

	// Frame loop driven by real ticks so it animates while GameSpeed pauses the
	// engine (Engine.TimeScale = 0), which Tweens freeze on.
	private async Task FadeAlphaAsync(CanvasItem target, float targetAlpha, float duration)
	{
		float startAlpha = target.Modulate.A;
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
}
