shader_type canvas_item;
render_mode blend_mix;

uniform vec4 line_color : source_color = vec4(0.85, 0.85, 0.85, 1.0);
uniform vec4 bg_color : source_color = vec4(0.0, 0.0, 0.0, 0.0);
// grid cell size in world units (pixels)
uniform float grid_cell_size : hint_range(10.0, 500.0) = 300.0;
// core line thickness in screen pixels
uniform float line_thickness : hint_range(0.005, 1.0) = 0.5;
// glow halo width in screen pixels (0 = no glow)
uniform float glow_width : hint_range(0.0, 20.0) = 6.0;
// glow opacity multiplier
uniform float glow_strength : hint_range(0.0, 1.0) = 0.18;
// intersection dot radius in screen pixels (0 = no dots)
uniform float dot_size : hint_range(0.0, 12.0) = 2.5;
// amount of per-line waviness in world pixels
uniform float wobble : hint_range(0.0, 50.0) = 8.0;
// how much cell brightness varies (0 = uniform, 1 = max variation)
uniform float cell_variation : hint_range(0.0, 1.0) = 0.35;
uniform float opacity : hint_range(0.0, 1.0) = 1.0;

uniform vec2 camera_position = vec2(0.0, 0.0);
uniform float camera_zoom = 1.0;

float hash(vec2 p) {
	p = fract(p * vec2(127.1, 311.7));
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
	vec2 viewport_size = 1.0 / SCREEN_PIXEL_SIZE;
	vec2 world_pos = camera_position + (SCREEN_UV - 0.5) * viewport_size / camera_zoom;

	// Per-line wobble: vertical lines waver in x based on y, horizontal lines waver in y based on x
	float grid_cell_size_x = grid_cell_size * 4.0;
	
	vec2 grid_idx = floor(world_pos / grid_cell_size);
	float wx = sin(world_pos.y / grid_cell_size_x * 6.14159 + hash(vec2(grid_idx.x, 0.0)) * 6.28) * wobble;
	float wy = sin(world_pos.x / grid_cell_size * .14159 + hash(vec2(0.0, grid_idx.y)) * 6.28) * wobble;
	vec2 wobbled = world_pos + vec2(wx, wy);

	float cell_px = grid_cell_size * camera_zoom;
	vec2 cell = fract(wobbled / grid_cell_size);

	float aa = 1.0 / cell_px;
	float lw = line_thickness / cell_px;
	float gw = (line_thickness + glow_width) / cell_px;

	// Anti-aliased core lines
	float lx = max(smoothstep(lw + aa, lw, cell.x), smoothstep(1.0 - lw - aa, 1.0 - lw, cell.x));
	float ly = max(smoothstep(lw + aa, lw, cell.y), smoothstep(1.0 - lw - aa, 1.0 - lw, cell.y));
	float core = max(lx, ly);

	// Soft glow halo
	float gx = max(smoothstep(gw + aa, gw, cell.x), smoothstep(1.0 - gw - aa, 1.0 - gw, cell.x));
	float gy = max(smoothstep(gw + aa, gw, cell.y), smoothstep(1.0 - gw - aa, 1.0 - gw, cell.y));
	float glow = max(gx, gy) * glow_strength;

	// Circular intersection dots (on undistorted grid for clean placement)
	vec2 orig_cell = fract(world_pos / grid_cell_size);
	vec2 corner_uv = orig_cell - round(orig_cell);
	float corner_dist_px = length(corner_uv) * cell_px;
	float dot = smoothstep(dot_size + 1.0, dot_size - 1.0, corner_dist_px);

	float intensity = clamp(max(max(core, glow), dot), 0.0, 1.0);

	// Per-cell brightness variation
	float cell_bright = 1.0 - cell_variation * hash(grid_idx);

	vec4 col = mix(bg_color, line_color * cell_bright, intensity);
	COLOR = vec4(col.rgb, col.a * opacity);
}
