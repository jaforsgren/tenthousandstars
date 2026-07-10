using Godot;
using Tts.Config;
using Tts.Utils;

namespace Tts.Nodes;

/// <summary>
/// Editor-only tool script attached to SystemCircleNode.tscn.
/// Lets you preview any of the variant SVGs directly in the scene editor
/// by changing PreviewTextureIndex in the inspector.
/// At runtime this script is a no-op — SystemCircleNode.cs drives the sprite.
/// </summary>
[Tool]
public partial class SystemCircleNodePreview : Node2D
{
	private int _previewTextureIndex;

	[Export(PropertyHint.Range, "0,8")]
	public int PreviewTextureIndex
	{
		get => _previewTextureIndex;
		set
		{
			_previewTextureIndex = value;
			if (Engine.IsEditorHint())
				ApplyPreviewTexture();
		}
	}

	private void ApplyPreviewTexture()
	{
		var variantSprite = GetNodeOrNull<Sprite2D>("VariantVisual");
		if (variantSprite is null) return;

		var cfg = ConfigLoader.Load<SystemConfig>("res://config/system.json");
		if (cfg.SystemTexturePaths.Length == 0) return;

		var idx   = Mathf.Clamp(_previewTextureIndex, 0, cfg.SystemTexturePaths.Length - 1);
		var scale = idx < cfg.SystemTextureScales.Length ? cfg.SystemTextureScales[idx] : 0.2f;

		variantSprite.Texture = GD.Load<Texture2D>(cfg.SystemTexturePaths[idx]);
		variantSprite.Scale   = new Vector2(scale, scale);
	}
}
