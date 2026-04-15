shader_type canvas_item;
render_mode blend_add;

uniform float timeScaleFactor = 0.08;
uniform sampler2D noise_texture : repeat_enable, filter_linear;
uniform float brightness : hint_range(0.0, 3.0) = 1.6;
uniform float clouds_resolution : hint_range(0.0, 20.0) = 6.0;
uniform float clouds_intesity : hint_range(-0.06, 0.5) = 0.05;
uniform float waveyness : hint_range(0.0, 10.0) = 2.0;
uniform float fragmentation : hint_range(0.0, 100.0) = 5.0;
uniform float distortion : hint_range(0.0, 10.0) = 1.5;
uniform float clouds_alpha : hint_range(0.4, 0.6) = 0.5;
uniform float movement : hint_range(0.7, 2.0) = 1.3;
uniform float blur : hint_range(0.0, 10.0) = 1.4;
uniform float blur2 : hint_range(0.0, 0.01) = 0.01;
uniform float bg_speed = 0.3;
uniform float mid_speed = 0.6;
uniform float fg_speed = 1.0;
uniform vec4 fromColor : source_color = vec4(1.0, 0.7, 0.1, 1.0);
uniform vec4 toColor : source_color = vec4(1.0, 0.3, 0.0, 1.0);
uniform float colorChangeTimeScale : hint_range(0.0, 2.0) = 0.3;
uniform float circle_radius : hint_range(0.1, 0.5) = 0.48;
uniform float edge_softness : hint_range(0.0, 0.15) = 0.05;

float localTime() {
	return TIME * timeScaleFactor;
}

mat2 makem2(float theta) {
	float c = cos(theta);
	float s = sin(theta);
	return mat2(vec2(c, -s), vec2(s, c));
}

float noise(vec2 x) {
	return texture(noise_texture, x * blur2).x;
}

float noisey(vec2 y) {
	return texture(noise_texture, y * blur2).y;
}

vec2 gradn(vec2 p) {
	float ep = 0.10;
	float gradx = noise(vec2(p.x + ep, p.y)) - noise(vec2(p.x - ep, p.y));
	float grady = noisey(vec2(p.x, p.y + ep)) - noisey(vec2(p.x, p.y - ep));
	return vec2(gradx, grady);
}

float flow(vec2 p) {
	float z = 2.0;
	float rz = clouds_intesity;
	vec2 bp = p;
	for (float i = 1.0; i < 7.0; ++i) {
		p += localTime() * 0.6;
		bp += localTime() * 5.9;
		vec2 gr = gradn(i * p * 0.34 + localTime());
		gr *= makem2(localTime() * 6.0 - (.05 * p.x + .03 * p.y) * 90.0);
		p += gr * waveyness;
		rz += (sin(noise(p) * fragmentation) * distortion + clouds_alpha) / z;
		p = mix(bp, p, movement);
		z *= blur;
		p *= 2.0;
		bp *= 1.9;
	}
	return rz;
}

void fragment() {
	vec2 p = UV * clouds_resolution;
	p *= clouds_resolution;

	vec2 bg_p = p * bg_speed;
	vec2 mid_p = p * mid_speed;
	vec2 fg_p = p * fg_speed;

	float t = sin(TIME * colorChangeTimeScale) * 0.5 + 0.5;
	vec3 base_color = mix(fromColor.rgb, toColor.rgb, t);

	float bg_rz = flow(bg_p);
	float mid_rz = flow(mid_p);
	float fg_rz = flow(fg_p);

	vec3 col_bg = base_color / bg_rz;
	vec3 col_mid = base_color / mid_rz;
	vec3 col_fg = base_color / fg_rz;

	col_bg = pow(col_bg, vec3(brightness));
	col_mid = pow(col_mid, vec3(brightness));
	col_fg = pow(col_fg, vec3(brightness));

	vec3 final_col = col_bg + col_mid + col_fg;

	// center
	vec2 uv_center = UV - vec2(0.5);
	float dist = length(uv_center) * 2.0;
	float alpha = smoothstep(circle_radius, circle_radius - edge_softness, dist);
	
	vec4 center = vec4(final_col, alpha);
	
	center = center + center + center + center ;
	
	//glow
	vec2 uv_center_glow = UV - vec2(0.5);
	
	float dist_glow = length(uv_center) * 1.8;
	float glow_softness = 2.0;
	float glow_radius = 1.0;
	float alpha_glow = smoothstep(glow_radius, glow_radius - glow_softness, dist_glow);
	
	vec3 glow_col = final_col*final_col * final_col;
	
	vec4 glow = vec4( glow_col, alpha_glow);
	
	COLOR = glow + center;
}
