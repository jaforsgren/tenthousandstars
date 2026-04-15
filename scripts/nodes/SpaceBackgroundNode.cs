using Godot;

namespace Tts;

public partial class SpaceBackgroundNode : ColorRect
{
	private ShaderMaterial _material = null!;

	public override void _Ready()
	{
		_material = (ShaderMaterial)Material;
	}

	public override void _Process(double delta)
	{
		var camera = GetViewport().GetCamera2D();
		if (camera == null) return;
		_material.SetShaderParameter("camera_position", camera.GlobalPosition);
		_material.SetShaderParameter("camera_zoom", camera.Zoom.X);
	}
}
