using Godot;

namespace Tts.Nodes;

[Tool]
public partial class ColorRemapNode : ColorRect
{
	[Export] public Color ShadowColor    { get => _shadowColor;    set { _shadowColor = value;    Apply(); } }
	[Export] public Color HighlightColor { get => _highlightColor; set { _highlightColor = value; Apply(); } }

	[Export(PropertyHint.Range, "0.0,1.0")]
	public float Strength { get => _strength; set { _strength = value; Apply(); } }

	private Color _shadowColor    = new(0.05f, 0.0f, 0.15f, 1.0f);
	private Color _highlightColor = new(0.8f, 0.9f, 1.0f, 1.0f);
	private float _strength       = 0.0f;

	public override void _Ready()
	{
		SyncFromMaterial();
	}

	private void SyncFromMaterial()
	{
		if (Material is not ShaderMaterial mat) return;
		_shadowColor    = mat.GetShaderParameter("shadow_color").AsColor();
		_highlightColor = mat.GetShaderParameter("highlight_color").AsColor();
		_strength       = mat.GetShaderParameter("strength").AsSingle();
		NotifyPropertyListChanged();
	}

	private void Apply()
	{
		if (!IsInsideTree()) return;
		if (Material is not ShaderMaterial mat) return;
		mat.SetShaderParameter("shadow_color",    _shadowColor);
		mat.SetShaderParameter("highlight_color", _highlightColor);
		mat.SetShaderParameter("strength",        _strength);
	}
}
