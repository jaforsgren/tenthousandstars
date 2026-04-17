shader_type canvas_item;
render_mode blend_add;

uniform vec4 color_top : source_color = vec4(0.05, 0.05, 0.2, 1.0);
uniform vec4 color_bottom : source_color = vec4(0.0, 0.0, 0.05, 1.0);
uniform float gradient_coverage : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	float t = clamp(UV.y / max(gradient_coverage, 0.001), 0.0, 1.0);
	COLOR = mix(color_top, color_bottom, t);
}
