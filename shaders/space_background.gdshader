shader_type canvas_item;

uniform bool stars_on = true;
uniform bool stars_flicker = true;
uniform float timeScaleFactor = 0.04;

uniform sampler2D noise_texture : repeat_enable, filter_linear;

uniform vec4 colour_muiltiplier : source_color = vec4(1.0);
uniform vec4 colour_muiltiplier2 : source_color = vec4(1.0);

uniform float brightness : hint_range(0.0, 3.0) = 1.2;
uniform float clouds_resolution : hint_range(0.0, 20.0) = 10.0;
uniform float clouds_intesity : hint_range(-0.06, 0.5) = 0.0;
uniform float waveyness : hint_range(0.0, 10.0) = 0.5;
uniform float fragmentation : hint_range(0.0, 100.0) = 7.0;
uniform float distortion : hint_range(0.0, 10.0) = 1.5;
uniform float clouds_alpha : hint_range(0.4, 0.6) = 0.5;
uniform float movement : hint_range(0.7, 2.0) = 1.3;
uniform float blur : hint_range(0.0, 10.0) = 1.4;
uniform float blur2 : hint_range(0.0, 0.01) = 0.01;

uniform float bg_speed = 0.1;
uniform float mid_speed = 0.5;
uniform float fg_speed = 1.0;

uniform vec4 fromColor : source_color = vec4(0.1, 0.1, 0.2, 1.0);
uniform vec4 toColor : source_color = vec4(0.2, 0.05, 0.05, 1.0);
uniform float colorChangeTimeScale : hint_range(0.0, 2.0) = 0.1;

uniform float star_density : hint_range(0.9, 1.0) = 0.965;
uniform float star_size : hint_range(0.01, 0.5) = 0.05;
uniform float star_flicker_speed : hint_range(0.1, 20.0) = 3.0;

// Set each frame from SpaceBackgroundNode.cs
uniform vec2 camera_position = vec2(0.0);
uniform float camera_zoom = 1.0;
uniform float parallax_pan_factor : hint_range(0.0, 0.5) = 0.01;
uniform float parallax_zoom_factor : hint_range(0.0, 1.0) = 0.05;

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
	float ep = 0.09;
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

float rand(vec2 st) {
	return fract(sin(dot(st.xy, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
	vec2 uv_pan = camera_position * SCREEN_PIXEL_SIZE * parallax_pan_factor;
	float zoom_scale = 1.0 + (camera_zoom - 1.0) * parallax_zoom_factor;
	vec2 parallax_uv = (UV - 0.5) / zoom_scale + 0.5 - uv_pan;

	vec2 p = parallax_uv * clouds_resolution;
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

	float stars = 0.0;
	vec2 star_uv = parallax_uv * 100.0 + localTime() * bg_speed * 20.0;
	vec2 grid_pos = floor(star_uv);
	vec2 local_pos = fract(star_uv);

	float rand_val = rand(grid_pos);
	if (rand_val > star_density) {
		float dist = distance(local_pos, vec2(0.5));
		float intensity = smoothstep(star_size, 0.0, dist);
		float flicker = stars_flicker ? (0.5 + 0.5 * sin(TIME * (rand_val * star_flicker_speed) + rand_val * 100.0)) : 1.0;
		stars = intensity * flicker * rand_val;
	}

	vec3 final_col = col_bg + col_mid + col_fg;

	if (stars_on) {
		final_col += vec3(stars);
	}

	COLOR = vec4(final_col * colour_muiltiplier.rgb, 1.0) * colour_muiltiplier2;
}
