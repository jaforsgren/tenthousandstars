shader_type canvas_item;
render_mode blend_mix;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, repeat_disable, filter_linear_mipmap;

uniform float brightness : hint_range(0.0, 2.0) = 1.0;
uniform float contrast : hint_range(0.0, 2.0) = 1.0;
uniform float saturation : hint_range(0.0, 2.0) = 1.0;
uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 0.0);

uniform vec4 radial_inner : source_color = vec4(1.0, 1.0, 1.0, 0.0);
uniform vec4 radial_outer : source_color = vec4(0.0, 0.0, 0.0, 0.0);
uniform float radial_strength : hint_range(0.0, 1.0) = 0.0;

uniform float vignette_strength : hint_range(0.0, 1.0) = 0.0;
uniform float vignette_softness : hint_range(0.01, 1.0) = 0.3;

vec3 overlay(vec3 base, vec3 blend) {
	return mix(
		2.0 * base * blend,
		1.0 - 2.0 * (1.0 - base) * (1.0 - blend),
		step(0.5, base)
	);
}

void fragment() {
	vec3 col = texture(SCREEN_TEXTURE, SCREEN_UV).rgb;

	col *= brightness;
	col = (col - 0.5) * contrast + 0.5;

	float lum = dot(col, vec3(0.299, 0.587, 0.114));
	col = mix(vec3(lum), col, saturation);

	col = mix(col, col * tint.rgb, tint.a);

	vec2 viewport_size = 1.0 / SCREEN_PIXEL_SIZE;
	float aspect = viewport_size.x / viewport_size.y;
	vec2 centered = (SCREEN_UV - 0.5) * vec2(aspect, 1.0);
	float dist = length(centered) * 2.0;

	float radial_dist = length(centered) * 1.5;
	vec4 radial = mix(radial_inner, radial_outer, clamp(radial_dist, 0.0, 1.0));
	col = mix(col, overlay(col, radial.rgb), radial.a * radial_strength);

	float vignette_dist = length(centered) * 1.4;
	float vignette = smoothstep(1.0, 1.0 - vignette_softness, vignette_dist);
	col *= mix(1.0, vignette, vignette_strength);

	COLOR = vec4(clamp(col, 0.0, 1.0), 1.0);
}
