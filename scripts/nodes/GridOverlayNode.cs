using Godot;

namespace Tts;

[Tool]
public partial class GridOverlayNode : ColorRect
{
	[Export] public Color LineColor      { get => _lineColor;      set { _lineColor = value;      Apply(); } }
	[Export] public Color BgColor        { get => _bgColor;        set { _bgColor = value;        Apply(); } }

	[Export(PropertyHint.Range, "10.0,500.0")]  public float GridCellSize   { get => _gridCellSize;   set { _gridCellSize = value;   Apply(); } }
	[Export(PropertyHint.Range, "0.005,1.0")]   public float LineThickness  { get => _lineThickness;  set { _lineThickness = value;  Apply(); } }
	[Export(PropertyHint.Range, "0.0,20.0")]    public float GlowWidth      { get => _glowWidth;      set { _glowWidth = value;      Apply(); } }
	[Export(PropertyHint.Range, "0.0,1.0")]     public float GlowStrength   { get => _glowStrength;   set { _glowStrength = value;   Apply(); } }
	[Export(PropertyHint.Range, "0.0,12.0")]    public float DotSize        { get => _dotSize;        set { _dotSize = value;        Apply(); } }
	[Export(PropertyHint.Range, "0.0,50.0")]    public float Wobble         { get => _wobble;         set { _wobble = value;         Apply(); } }
	[Export(PropertyHint.Range, "0.0,1.0")]     public float CellVariation  { get => _cellVariation;  set { _cellVariation = value;  Apply(); } }
	[Export(PropertyHint.Range, "0.0,1.0")]     public float Opacity        { get => _opacity;        set { _opacity = value;        Apply(); } }

	private Color _lineColor     = new(0.85f, 0.85f, 0.85f, 1.0f);
	private Color _bgColor       = new(0.0f, 0.0f, 0.0f, 0.0f);
	private float _gridCellSize  = 300.0f;
	private float _lineThickness = 0.5f;
	private float _glowWidth     = 6.0f;
	private float _glowStrength  = 0.18f;
	private float _dotSize       = 2.5f;
	private float _wobble        = 8.0f;
	private float _cellVariation = 0.35f;
	private float _opacity       = 1.0f;

	public override void _Ready()
	{
		SyncFromMaterial();
	}

	public override void _Process(double delta)
	{
		if (Material is not ShaderMaterial mat) return;
		var camera = GetViewport().GetCamera2D();
		if (camera == null) return;
		mat.SetShaderParameter("camera_position", camera.GlobalPosition);
		mat.SetShaderParameter("camera_zoom", camera.Zoom.X);
	}

	// Read the saved shader material values into backing fields so Inspector reflects them
	// and so Apply() doesn't overwrite tuned values on game start.
	private void SyncFromMaterial()
	{
		if (Material is not ShaderMaterial mat) return;
		_lineColor     = mat.GetShaderParameter("line_color").AsColor();
		_bgColor       = mat.GetShaderParameter("bg_color").AsColor();
		_gridCellSize  = mat.GetShaderParameter("grid_cell_size").AsSingle();
		_lineThickness = mat.GetShaderParameter("line_thickness").AsSingle();
		_glowWidth     = mat.GetShaderParameter("glow_width").AsSingle();
		_glowStrength  = mat.GetShaderParameter("glow_strength").AsSingle();
		_dotSize       = mat.GetShaderParameter("dot_size").AsSingle();
		_wobble        = mat.GetShaderParameter("wobble").AsSingle();
		_cellVariation = mat.GetShaderParameter("cell_variation").AsSingle();
		_opacity       = mat.GetShaderParameter("opacity").AsSingle();
		NotifyPropertyListChanged();
	}

	private void Apply()
	{
		// Guard: don't apply during scene loading (before node is in tree).
		// The ShaderMaterial resource already holds the saved values at that point.
		if (!IsInsideTree()) return;
		if (Material is not ShaderMaterial mat) return;
		mat.SetShaderParameter("line_color",     _lineColor);
		mat.SetShaderParameter("bg_color",       _bgColor);
		mat.SetShaderParameter("grid_cell_size", _gridCellSize);
		mat.SetShaderParameter("line_thickness", _lineThickness);
		mat.SetShaderParameter("glow_width",     _glowWidth);
		mat.SetShaderParameter("glow_strength",  _glowStrength);
		mat.SetShaderParameter("dot_size",       _dotSize);
		mat.SetShaderParameter("wobble",         _wobble);
		mat.SetShaderParameter("cell_variation", _cellVariation);
		mat.SetShaderParameter("opacity",        _opacity);
	}
}
