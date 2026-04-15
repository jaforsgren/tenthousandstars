shader_type canvas_item;

uniform vec4 color_inner : source_color = vec4(0.2, 0.5, 0.9, 1.0);
uniform vec4 color_outer : source_color = vec4(0.05, 0.1, 0.3, 1.0);
uniform float circle_radius : hint_range(0.01, 0.5) = 0.45;
uniform float gradient_scale : hint_range(0.1, 5.0) = 1.0;
// Normalized direction from planet toward system center — updated each frame from PlanetNode.cs
uniform vec2 gradient_direction = vec2(0.0, -1.0);

void fragment() {
	vec2 uv = UV - vec2(0.5);
	float dist = length(uv);
	float alpha = step(dist, circle_radius);

	// Linear gradient projected onto the toward-center axis
	float t = clamp(dot(uv, gradient_direction) / circle_radius * gradient_scale * 0.5 + 0.5, 0.0, 1.0);

	// t=1 faces center (color_inner), t=0 faces away (color_outer)
	vec4 col = mix(color_outer, color_inner, t);
	COLOR = vec4(col.rgb, col.a * alpha);
}
