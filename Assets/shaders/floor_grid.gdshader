shader_type canvas_item;

uniform vec4 base_color : source_color = vec4(0.09, 0.11, 0.14, 1.0);
uniform vec4 line_color : source_color = vec4(0.16, 0.20, 0.26, 1.0);
uniform vec4 dot_color : source_color = vec4(0.24, 0.32, 0.42, 1.0);
uniform float tile_size = 96.0;
uniform float line_thickness = 2.0;
uniform float edge_fade = 1.0;

varying vec2 world_pos;

float hash21(vec2 p) {
	p = fract(p * vec2(234.34, 435.345));
	p += dot(p, p + 34.23);
	return fract(p.x * p.y);
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec2 pos = world_pos;
	vec2 tile_id = floor(pos / tile_size);
	vec2 tile_uv = fract(pos / tile_size);
	float tile_noise = hash21(tile_id);
	
	// Distance to grid lines
	vec2 d = min(tile_uv, 1.0 - tile_uv) * tile_size;
	float dist_to_edge = min(d.x, d.y);
	float edge = smoothstep(0.0, line_thickness, dist_to_edge);
	
	// Cross / dot at intersections
	float corner_dist = length(min(tile_uv, 1.0 - tile_uv) * tile_size);
	float dot_mask = 1.0 - smoothstep(2.0, 5.0, corner_dist);
	
	// Subtle inner bevel
	float bevel = smoothstep(line_thickness, line_thickness + 16.0, dist_to_edge) * 0.08;
	
	// Composite color
	vec3 col = base_color.rgb + (tile_noise - 0.5) * 0.03 + bevel;
	col = mix(line_color.rgb, col, edge);
	col = mix(col, dot_color.rgb, dot_mask * 0.6);
	
	// Smooth fadeout at arena boundaries using UV
	vec2 uv_dist = abs(UV - 0.5) * 2.0;
	float border_fade = 1.0 - smoothstep(0.85, 1.0, max(uv_dist.x, uv_dist.y));
	
	COLOR = vec4(col, base_color.a * mix(1.0, border_fade, edge_fade));
}
