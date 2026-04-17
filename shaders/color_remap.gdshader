shader_type canvas_item;
render_mode blend_mix;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform vec4 shadow_color : source_color = vec4(0.05, 0.0, 0.15, 1.0);
uniform vec4 highlight_color : source_color = vec4(0.8, 0.9, 1.0, 1.0);
uniform float strength : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec3 col = texture(SCREEN_TEXTURE, SCREEN_UV).rgb;
	float lum = dot(col, vec3(0.299, 0.587, 0.114));
	vec3 remapped = mix(shadow_color.rgb, highlight_color.rgb, lum);
	COLOR = vec4(mix(col, remapped, strength), 1.0);
}
