shader_type canvas_item;

uniform float roughness : hint_range(0.0, 2.0) = 0.6;

float hash(float n) {
	return fract(sin(n * 127.1) * 43758.5453);
}

float smooth_noise(float x) {
	float i = floor(x);
	float f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(hash(i), hash(i + 1.0), f);
}

void fragment() {
	float freq = 12.0;
	float dx = (smooth_noise(UV.y * freq) - 0.5) * TEXTURE_PIXEL_SIZE.x * roughness * 4.0;
	float dy = (smooth_noise(UV.x * freq + 53.7) - 0.5) * TEXTURE_PIXEL_SIZE.y * roughness * 4.0;
	COLOR = texture(TEXTURE, UV + vec2(dx, dy));
}
