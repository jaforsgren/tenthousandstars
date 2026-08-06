using System;
using System.Linq;
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
}