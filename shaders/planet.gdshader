shader_type canvas_item;

uniform sampler2D noise_texture;

uniform vec4 ocean_color : source_color = vec4(0.08, 0.20, 0.55, 1.0);
uniform vec4 land_color : source_color = vec4(0.22, 0.65, 0.30, 1.0);
uniform vec4 mountain_color : source_color = vec4(0.45, 0.45, 0.42, 1.0);
uniform vec4 atmosphere_color : source_color = vec4(0.55, 0.75, 1.0, 1.0);

uniform float radius = 0.48;
uniform vec2 light_direction = vec2(-0.5, -1.0);

uniform float continent_scale = 3.5;
uniform float continent_threshold = 0.52;

void fragment()
{
    vec2 p = (UV - vec2(0.5)) / radius;

    float r2 = dot(p, p);
    if (r2 > 1.0)
        discard;

    // Sphere normal
    float z = sqrt(1.0 - r2);
    vec3 normal = normalize(vec3(p, z));

    // Lighting
    vec3 light = normalize(vec3(light_direction, 0.8));
    float diffuse = max(dot(normal, light), 0.15);

    // Warp the noise a little so continents aren't blobs
    vec2 warp = (texture(noise_texture, UV * 8.0).rg - 0.5) * 0.05;

    float n =
        texture(noise_texture, (UV + warp) * continent_scale).r * 0.7 +
        texture(noise_texture, (UV + warp) * continent_scale * 2.3).r * 0.3;

    // Continents
    float land = smoothstep(
        continent_threshold - 0.03,
        continent_threshold + 0.03,
        n
    );

    vec3 color = mix(ocean_color.rgb, land_color.rgb, land);

    // Mountains
    float mountains = smoothstep(0.78, 0.90, n) * land;
    color = mix(color, mountain_color.rgb, mountains);

    // Spherical lighting
    color *= diffuse;

    // Atmospheric rim
    float rim = pow(1.0 - normal.z, 3.0);
    color += atmosphere_color.rgb * rim * 0.35;

    COLOR = vec4(color, 1.0);
}