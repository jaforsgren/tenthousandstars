shader_type canvas_item;

void fragment() {
	COLOR = vec4(0.00, 0.00, 0.00, (1.0 - UV.y) * 0.96);
}
