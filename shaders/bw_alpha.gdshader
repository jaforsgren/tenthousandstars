shader_type canvas_item;

// Converts the greyscale luminance of each pixel into its alpha channel.
// White (luma=1) → fully opaque. Black (luma=0) → fully transparent.
// Works on any CanvasItem: COLOR is pre-filled with the sampled texture before
// this fragment runs.
void fragment() {
	float luma = dot(COLOR.rgb, vec3(0.299, 0.587, 0.114));
	COLOR.a = luma;
}
