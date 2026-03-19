using Godot;

namespace Tts;

public record ColorData(float R, float G, float B, float A)
{
	public Color ToColor() => new(R, G, B, A);
}
