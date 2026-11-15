// Shared fragment body: desktop GL 3.30 (stills) and WebGL2 (tuner page) prepend their own #version line.
uniform sampler2D uMain;
uniform vec2 uRes;
uniform float uTime;
uniform vec3 uCenter;      // planet centre relative to the camera, world units
uniform float uRadius;
uniform vec3 uLight;
uniform vec3 uTint;       // material colours arrive as authored (sRGB), as Unity stores them
uniform vec3 uAtmo;
uniform vec3 uSky;
uniform sampler2D uBackdrop; // optional in-game frame behind the planet
uniform float uUseBackdrop;
uniform float uTilt;       // degrees
uniform float uLongitude;  // degrees
uniform float uRotSpeed;   // radians per second

uniform float uJet;        // band streaming: peak extra spin of a band, radians per second
uniform float uJetBands;   // how many alternating bands, pole to pole
uniform float uEddy;       // storm churn: eddy drift, radians per second
uniform float uEddyScale;  // eddy size: higher is smaller eddies
uniform float uFlowCycle;  // seconds before the flow re-anchors to the painted map

uniform float uDeck;       // high cloud deck opacity
uniform float uDeckCover;  // how much of the second layer counts as cloud, 0..1
uniform float uDeckSpeed;  // deck drift over the surface, radians per second
uniform float uDeckShadow; // how dark the deck's shadow is on the clouds below

uniform float uRimPower;      // inner rim tightness: higher hugs the edge
uniform float uRimIntensity;  // inner rim brightness
uniform float uHaloWidth;     // glow reach beyond the edge, as a fraction of the radius
uniform float uHaloIntensity; // glow brightness beyond the edge
uniform float uHaloFalloff;   // higher pulls the glow in toward the edge
uniform float uNightGlow;     // how much glow survives on the unlit side, 0..1

out vec4 fragColor;

const float PI = 3.14159265359;
const float TWO_PI = 6.28318530718;

vec3 toLinear(vec3 c) { return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c)); }
vec3 toSrgb(vec3 c)
{
    c = clamp(c, 0.0, 1.0);
    return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
}

// The game's grade: +0.52 stops of post exposure, then ACES (Stephen Hill's fit).
vec3 grade(vec3 c)
{
    c *= 1.434;
    c = mat3(0.59719, 0.07600, 0.02840, 0.35458, 0.90834, 0.13383, 0.04823, 0.01566, 0.83777) * c;
    c = (c * (c + 0.0245786) - 0.000090537) / (c * (0.983729 * c + 0.4329510) + 0.238081);
    c = mat3(1.60475, -0.10208, -0.00327, -0.53108, 1.10813, -0.07276, -0.07367, -0.00605, 1.07602) * c;
    return clamp(c, 0.0, 1.0);
}

float hash13(vec3 p)
{
    p = fract(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return fract((p.x + p.y) * p.z);
}

// Value noise and its gradient.
vec4 noised(vec3 x)
{
    vec3 i = floor(x);
    vec3 w = fract(x);
    vec3 u = w * w * w * (w * (w * 6.0 - 15.0) + 10.0);
    vec3 du = 30.0 * w * w * (w * (w - 2.0) + 1.0);
    float a = hash13(i);
    float b = hash13(i + vec3(1, 0, 0));
    float c = hash13(i + vec3(0, 1, 0));
    float d = hash13(i + vec3(1, 1, 0));
    float e = hash13(i + vec3(0, 0, 1));
    float f = hash13(i + vec3(1, 0, 1));
    float g = hash13(i + vec3(0, 1, 1));
    float h = hash13(i + vec3(1, 1, 1));
    float k1 = b - a, k2 = c - a, k3 = e - a;
    float k4 = a - b - c + d, k5 = a - c - e + g, k6 = a - b - e + f;
    float k7 = -a + b + c - d + e - f - g + h;
    return vec4(a + k1 * u.x + k2 * u.y + k3 * u.z + k4 * u.x * u.y + k5 * u.y * u.z + k6 * u.z * u.x +
        k7 * u.x * u.y * u.z,
        du * vec3(k1 + k4 * u.y + k6 * u.z + k7 * u.y * u.z,
            k2 + k5 * u.z + k4 * u.x + k7 * u.z * u.x,
            k3 + k6 * u.x + k5 * u.y + k7 * u.x * u.y));
}

vec3 spinY(vec3 s, float angle)
{
    float c = cos(angle), n = sin(angle);
    return vec3(s.x * c - s.z * n, s.y, s.z * c + s.x * n);
}

vec2 mapUv(vec3 q)
{
    return vec2(atan(q.x, -q.z) / TWO_PI + 0.5, asin(clamp(q.y, -1.0, 1.0)) / PI + 0.5);
}

vec3 sampleMap(vec3 q)
{
    vec2 uv = mapUv(q);
    // The map wraps behind the planet; take the derivative of the unwrapped longitude there.
    vec2 uvAlt = vec2(fract(uv.x + 0.5) - 0.5, uv.y);
    vec2 dx = abs(dFdx(uv.x)) < abs(dFdx(uvAlt.x)) ? dFdx(uv) : dFdx(uvAlt);
    vec2 dy = abs(dFdy(uv.x)) < abs(dFdy(uvAlt.x)) ? dFdy(uv) : dFdy(uvAlt);
    return toLinear(textureGrad(uMain, uv, dx, dy).rgb);
}

float stars(vec2 px)
{
    vec2 cell = floor(px / 9.0);
    float pick = hash13(vec3(cell, 7.0));
    vec2 at = (cell + 0.2 + 0.6 * vec2(hash13(vec3(cell, 1.0)), hash13(vec3(cell, 2.0)))) * 9.0;
    float size = 0.6 + 1.1 * hash13(vec3(cell, 3.0));
    return step(0.93, pick) * (1.0 - smoothstep(0.0, size, length(px - at))) * (0.25 + 0.75 * hash13(vec3(cell, 4.0)));
}

// Body-fixed direction of a view-space surface normal.
vec3 bodyDirection(vec3 normal)
{
    float tilt = radians(uTilt);
    vec3 surface = vec3(normal.x * cos(tilt) + normal.y * sin(tilt),
        normal.y * cos(tilt) - normal.x * sin(tilt), normal.z);
    return spinY(surface, radians(uLongitude) - uTime * uRotSpeed);
}

float deckMask(vec3 colour)
{
    float low = mix(0.55, 0.02, uDeckCover);
    return smoothstep(low, low + 0.22, max(colour.r, max(colour.g, colour.b)));
}

vec3 deckDirection(vec3 q)
{
    // A second reading of the same painted map: mirrored, offset and drifting, so it never lines up.
    return spinY(vec3(q.x, -q.y, q.z), 2.4 - uTime * uDeckSpeed);
}

void main()
{
    vec2 screen = gl_FragCoord.xy / uRes * 2.0 - 1.0;
    // The backdrop frame is already graded; the flat sky is not.
    vec3 sky = uUseBackdrop > 0.5 ? toLinear(texture(uBackdrop, gl_FragCoord.xy / uRes).rgb)
        : grade(toLinear(uSky) + vec3(0.9, 0.95, 1.0) * stars(gl_FragCoord.xy) * 0.6);
    vec3 atmoColour = toLinear(uAtmo);
    vec2 view = screen * vec2(uRes.x / uRes.y, 1.0) * 0.3639702343;
    vec3 ray = normalize(vec3(view, 1.0));
    vec3 toCenter = uCenter / uRadius;
    float along = dot(toCenter, ray);
    vec3 nearest = ray * along - toCenter;
    float distanceSquared = dot(nearest, nearest);
    float outerRadius = 1.0 + uHaloWidth;
    float outerRadiusSquared = outerRadius * outerRadius;
    if (along < 0.0 || distanceSquared > outerRadiusSquared)
    {
        fragColor = vec4(toSrgb(sky), 1.0);
        return;
    }

    float surfaceDepth = sqrt(max(0.0, 1.0 - distanceSquared));
    vec3 normal = normalize(nearest - ray * surfaceDepth);
    vec3 light = normalize(uLight);
    float daylight = clamp(dot(normal, light), 0.0, 1.0);

    vec3 q = bodyDirection(normal);

    // Flow: bands stream along latitude, eddies circle around noise cells. Two readings of the map,
    // half a cycle apart, cross-fade so the stretch never builds past one cycle.
    vec4 field = noised(q * uEddyScale);
    vec3 eddy = cross(q, field.yzw) * uEddy / max(uEddyScale, 0.001);
    float latitude = asin(clamp(q.y, -1.0, 1.0));
    float jet = uJet * cos(uJetBands * latitude);
    float cycle = uTime / uFlowCycle + field.x * 0.6;
    float phaseA = fract(cycle);
    float phaseB = fract(cycle + 0.5);
    float tA = (phaseA - 0.5) * uFlowCycle;
    float tB = (phaseB - 0.5) * uFlowCycle;
    vec3 colourA = sampleMap(spinY(normalize(q - eddy * tA), -jet * tA));
    vec3 colourB = sampleMap(spinY(normalize(q - eddy * tB), -jet * tB));
    vec3 albedo = mix(colourA, colourB, abs(2.0 * phaseA - 1.0));

    // High deck: brighter strokes of a drifting second reading, with a shadow thrown away from the light.
    if (uDeck > 0.0)
    {
        vec3 deck = sampleMap(deckDirection(q));
        float mask = deckMask(deck) * uDeck;
        vec3 towardLight = bodyDirection(normalize(normal + light * 0.02));
        float shadow = deckMask(sampleMap(deckDirection(towardLight))) * uDeck * uDeckShadow;
        albedo *= 1.0 - shadow * (1.0 - mask);
        albedo = mix(albedo, deck * 1.3 + 0.05, mask);
    }
    albedo *= toLinear(uTint);

    float glowLight = uNightGlow + daylight * (1.0 - uNightGlow);
    float grazing = pow(1.0 - clamp(dot(normal, -ray), 0.0, 1.0), uRimPower);
    vec3 atmosphere = atmoColour * grazing * uRimIntensity * glowLight;
    vec3 body = albedo * (0.08 + daylight * 0.92) + atmosphere;

    float falloff = pow(1.0 - smoothstep(1.0, outerRadiusSquared, distanceSquared), uHaloFalloff);
    vec3 halo = atmoColour * glowLight * falloff * uHaloIntensity;
    float haloAlpha = falloff * 0.35;
    float coverage = clamp((1.0 - distanceSquared) / fwidth(distanceSquared) + 0.5, 0.0, 1.0);
    vec3 colour = mix(halo, body, coverage);
    float alpha = mix(haloAlpha, 1.0, coverage);
    fragColor = vec4(toSrgb(sky * (1.0 - alpha) + grade(colour)), 1.0);
}
