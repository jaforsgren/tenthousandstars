shader_type canvas_item;

uniform sampler2D noise_texture : repeat_enable, filter_linear;
uniform float noise_scale    : hint_range(0.1, 8.0)  = 1.0;
uniform float threshold      : hint_range(0.0, 1.0)  = 0.3;
uniform float edge_softness  : hint_range(0.001, 0.5) = 0.15;
uniform float noise_strength : hint_range(0.0, 1.0)  = 0.5;
// set > 0 to slowly animate the noise field
uniform float time_scale     : hint_range(0.0, 1.0)  = 0.0;

void fragment() {
	vec4 sprite = texture(TEXTURE, UV);

	vec2 noise_uv = UV * noise_scale + TIME * time_scale * 0.05;
	float n       = texture(noise_texture, noise_uv).r;

	float mask  = smoothstep(threshold - edge_softness, threshold + edge_softness, n);
	float alpha = sprite.a * mix(1.0, mask, noise_strength);

	COLOR = vec4(sprite.rgb, alpha);
}
