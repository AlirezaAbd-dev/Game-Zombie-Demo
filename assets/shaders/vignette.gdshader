shader_type canvas_item;

uniform vec4 vignette_color : source_color = vec4(0.01, 0.02, 0.04, 0.55);
uniform float outer_radius : hint_range(0.5, 2.0) = 1.25;
uniform float inner_radius : hint_range(0.0, 1.0) = 0.5;

void fragment() {
	vec2 uv = (UV - 0.5) * 2.0;
	float d = length(uv);
	float mask = smoothstep(inner_radius, outer_radius, d);
	COLOR = vec4(vignette_color.rgb, vignette_color.a * mask);
}
