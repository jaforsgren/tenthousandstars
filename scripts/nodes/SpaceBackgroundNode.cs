using Godot;

namespace Tts.Nodes;

[Tool]
public partial class SpaceBackgroundNode : ColorRect
{
	// How much the background pans relative to camera movement (0 = locked, 1 = moves with camera)
	[Export(PropertyHint.Range, "0.0,1.0")]
	public float ParallaxPanFactor
	{
		get => _parallaxPanFactor;
		set { _parallaxPanFactor = value; Apply(); }
	}

	// How much the background scales relative to camera zoom (0 = no zoom effect, 1 = full scale)
	[Export(PropertyHint.Range, "0.0,1.0")]
	public float ParallaxZoomFactor
	{
		get => _parallaxZoomFactor;
		set { _parallaxZoomFactor = value; Apply(); }
	}

	private float _parallaxPanFactor = 0.05f;
	private float _parallaxZoomFactor = 0.1f;
	private ShaderMaterial? _material;

	public override void _Ready()
	{
		_material = Material as ShaderMaterial;
		SyncFromMaterial();
	}

	public override void _Process(double delta)
	{
		if (_material == null) return;
		var camera = GetViewport().GetCamera2D();
		if (camera == null) return;
		_material.SetShaderParameter("camera_position", camera.GlobalPosition);
		_material.SetShaderParameter("camera_zoom", camera.Zoom.X);
	}

	private void SyncFromMaterial()
	{
		if (_material == null) return;
		_parallaxPanFactor  = _material.GetShaderParameter("parallax_pan_factor").AsSingle();
		_parallaxZoomFactor = _material.GetShaderParameter("parallax_zoom_factor").AsSingle();
		NotifyPropertyListChanged();
	}

	private void Apply()
	{
		if (!IsInsideTree()) return;
		if (_material == null) return;
		_material.SetShaderParameter("parallax_pan_factor",  _parallaxPanFactor);
		_material.SetShaderParameter("parallax_zoom_factor", _parallaxZoomFactor);
	}
}
