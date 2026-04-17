shader_type canvas_item;

uniform vec3 bg_color = vec3(0.07, 0.08, 0.09);
uniform float bg_alpha : hint_range(0.0, 1.0) = 1.0;

uniform vec3 line_color = vec3(0.7, 0.7, 0.7);
uniform float grid_alpha : hint_range(0.0, 1.0) = 1.0;

uniform vec3 accent = vec3(0.1, 0.8, 0.6);

uniform float grid_scale = 40.0;

float circle(vec2 uv, vec2 center, float radius, float thickness) {
    float d = length(uv - center);
    return smoothstep(radius + thickness, radius, d) -
           smoothstep(radius, radius - thickness, d);
}

float line(vec2 uv, vec2 a, vec2 b, float thickness) {
    vec2 pa = uv - a;
    vec2 ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    float d = length(pa - ba * h);
    return smoothstep(thickness, 0.0, d);
}

void fragment() {
    vec2 uv = VERTEX;
    vec2 center = vec2(0.5);

    // --- BACKGROUND ---
    vec3 bg = bg_color;

    // --- GRID / LINES ---
    float mask = 0.0;

    // grid
    vec2 grid = fract(uv * grid_scale);
    mask += (step(grid.x, 0.01) + step(grid.y, 0.01)) * 0.15;

    // circles
    mask += circle(uv, center, 0.35, 0.0006) * 0.6;
    mask += circle(uv, center, 0.25, 0.0005) * 0.6;
    mask += circle(uv, center, 0.15, 0.0005) * 0.6;

    vec2 top = center + vec2(0.0, -0.28);
    vec2 bot = center + vec2(0.0,  0.28);

    mask += circle(uv, top, 0.25, 0.002) * 0.4;
    mask += circle(uv, bot, 0.25, 0.002) * 0.4;

    mask += line(uv, vec2(0.5, 0.0), vec2(0.5, 1.0), 0.0015) * 0.5;

    vec2 left = center + vec2(-0.18, 0.0);
    vec2 right = center + vec2(0.18, 0.0);

    mask += circle(uv, left, 0.03, 0.002) * 0.5;
    mask += circle(uv, right, 0.03, 0.002) * 0.5;

    mask += circle(uv, center, 0.48, 0.001) * 0.2;

    // --- ACCENT DOT ---
    float dot = circle(uv, center, 0.01, 0.001);

    // --- VIGNETTE ---
    float v = smoothstep(0.9, 0.4, distance(uv, center));

    // --- FINAL COMPOSE ---
    vec3 color = bg;
    color = mix(color, line_color, mask * grid_alpha);
    color = mix(color, accent, dot);

    float alpha = bg_alpha;
    alpha += mask * grid_alpha; // grid adds opacity
    alpha = clamp(alpha, 0.0, 1.0);

    color *= v;

    COLOR = vec4(color, alpha);
}