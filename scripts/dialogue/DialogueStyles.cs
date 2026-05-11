#nullable enable

using Godot;

namespace Tts;

// Values the theme system cannot express:
//   - BBCode hex strings embedded in markup at runtime
//   - Colors applied conditionally based on game data (skill check result)
//   - ColorRect.Color (not a theme property)
//
// Everything else (font sizes, label colors) lives in themes/dialog_theme.tres.
public static class DialogueStyles
{
	// BBCode hex for RichTextLabel markup (colour applied inline, not via theme)
	public const string PlayerAccentHex = "#e8a020";

	// Skill-check result — applied at runtime based on success/failure
	public static readonly Color SkillSuccess = new(0.3f, 1.0f, 0.3f, 1.0f);
	public static readonly Color SkillFailure = new(1.0f, 0.3f, 0.3f, 1.0f);

	// Separator lines (ColorRect.Color, not a theme property)
	public static readonly Color Divider = new(0.25f, 0.25f, 0.25f, 0.6f);
}
