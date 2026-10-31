shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
uniform vec4 vignette_color : source_color = vec4(0.01, 0.02, 0.04, 0.55);
uniform float outer_radius : hint_range(0.5, 2.0) = 1.25;
uniform float inner_radius : hint_range(0.0, 1.0) = 0.5;
uniform float aberration_strength : hint_range(0.0, 0.05) = 0.0;

void fragment() {
	vec2 uv = (UV - 0.5) * 2.0;
	float d = length(uv);
	float mask = smoothstep(inner_radius, outer_radius, d);

	if (aberration_strength > 0.0001) {
		vec2 dir = SCREEN_UV - vec2(0.5);
		float r = texture(screen_texture, SCREEN_UV + dir * aberration_strength).r;
		float g = texture(screen_texture, SCREEN_UV).g;
		float b = texture(screen_texture, SCREEN_UV - dir * aberration_strength).b;
		vec3 scene_col = vec3(r, g, b);
		COLOR = vec4(mix(scene_col, vignette_color.rgb, vignette_color.a * mask), 1.0);
	} else {
		COLOR = vec4(vignette_color.rgb, vignette_color.a * mask);
	}
}
