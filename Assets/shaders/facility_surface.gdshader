shader_type canvas_item;

// Static, world-scaled surfaces. Lighting comes from the scene's real lights.
uniform int surface_type = 1;
uniform vec4 base_color : source_color = vec4(0.25, 0.26, 0.25, 1.0);
uniform vec4 joint_color : source_color = vec4(0.08, 0.09, 0.09, 1.0);
uniform float wear_amount : hint_range(0.0, 1.0) = 0.4;
varying vec2 world_pos;

float hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0)), f.x), f.y);
}

float joint(vec2 p, vec2 size, float width) {
	vec2 d = min(mod(p, size), size - mod(p, size));
	return 1.0 - smoothstep(width, width + 0.8, min(d.x, d.y));
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec2 p = world_pos;
	float grain = hash(floor(p / 1.5)) - 0.5;
	float broad = noise(p / 180.0);
	float stains = smoothstep(0.51, 0.82, noise(p / 95.0)) * wear_amount;
	vec3 col = base_color.rgb;
	if (surface_type == 0) { // Asphalt: aggregate and uneven damp patches, no tile grid.
		col += grain * 0.035 + (broad - 0.5) * 0.05;
		col *= 1.0 - stains * 0.4;
		float stones = step(0.965, hash(floor(p / 3.0)));
		col += stones * 0.02;
	} else if (surface_type == 1) { // Cast concrete with large expansion joints and fine cracks.
		col += grain * 0.035 + (broad - 0.5) * 0.06;
		col = mix(col, joint_color.rgb, joint(p, vec2(320.0, 256.0), 0.7) * 0.55);
		float fissure = 1.0 - smoothstep(0.002, 0.006, abs(noise(p / 46.0) - 0.48));
		fissure *= smoothstep(0.55, 0.82, noise(p / 280.0 + vec2(17.0, 3.0)));
		col *= 1.0 - fissure * wear_amount * 0.35 - stains * 0.15;
	} else if (surface_type == 2) { // Bolted steel plates and shallow tread, without emissive seams.
		vec2 plate = mod(p, vec2(192.0, 128.0));
		float edge = joint(p, vec2(192.0, 128.0), 1.0);
		float tread = 1.0 - smoothstep(0.6, 1.6, abs(mod(p.x + p.y * 1.5, 16.0) - 8.0));
		col += grain * 0.02 + tread * 0.018 - stains * 0.04;
		col = mix(col, joint_color.rgb, edge * 0.8);
		vec2 bolt_dist = abs(min(plate, vec2(192.0, 128.0) - plate) - vec2(8.0));
		float bolt = 1.0 - smoothstep(1.4, 2.5, length(bolt_dist));
		col = mix(col, vec3(0.09), bolt * 0.7);
	} else if (surface_type == 3) { // Institutional ceramic with recessed grout and varied tiles.
		float variation = hash(floor(p / 64.0)) - 0.5;
		col += variation * 0.04 + grain * 0.012 - stains * 0.035;
		col = mix(col, joint_color.rgb, joint(p, vec2(64.0), 0.8) * 0.8);
	} else if (surface_type == 4) { // Sealed laboratory vinyl with quiet weld lines and scuffs.
		col += grain * 0.014 + (broad - 0.5) * 0.025 - stains * 0.04;
		col = mix(col, joint_color.rgb, joint(p, vec2(192.0), 0.35) * 0.2);
		float scuff = smoothstep(0.75, 0.92, noise(p / vec2(7.0, 80.0)));
		col -= scuff * wear_amount * 0.025;
	} else { // Worn archive carpet: a close weave rather than luminous floor panels.
		float weave = mod(floor(p.x) + floor(p.y), 4.0) / 4.0;
		col += (weave - 0.5) * 0.026 + grain * 0.025 - stains * 0.04;
	}
	COLOR = vec4(max(col, vec3(0.0)), 1.0);
}
