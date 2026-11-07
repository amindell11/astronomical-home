// Gives the mixer the structure the owner balances by ear: routing, instance caps, neutral effects,
// the LowHealth snapshot's scope and the reverb sends. Every fader stays at 0 dB, the EQs and the
// Compressor start where they change nothing, and a value the owner has already set is never overwritten.
//   bus:/World/Weapons      every event:/Weapons/*   post-fader: Sidechain, Send to bus:/Reverb at -18 dB
//   bus:/World/Ships        every event:/Ships/*
//   bus:/World/Environment  every event:/World/*     post-fader: Send to bus:/Reverb at -18 dB
//   bus:/World              pre-fader: Compressor (2:1, 100 ms attack, threshold and makeup 0 dB),
//                           Multiband EQ whose band A is a 12 dB low-pass parked at 22 kHz
//   bus:/Music              pre-fader: flat 3-EQ; a sidechain modulator on the volume ducks 4 dB
//                           from the Weapons sidechain with a 1 s release
//   Master Bus              post-fader: Limiter at a -1 dB ceiling, then a Loudness Meter
//   snapshot:/LowHealth     scopes bus:/World volume to -10 dB and the Multiband EQ's band A to 1.5 kHz;
//                           an AHDSR on the snapshot's intensity, 0.5 s attack and release
//   every 3D event          pre-fader: the shared effect "Distance Low-Pass", a 12 dB low-pass whose
//                           cutoff follows the built-in Distance parameter in plane units: open to 25,
//                           500 Hz at 115. One curve, shared, so it tunes in one place.
// Instance caps in each event's macros: Weapons/*/Fire 8 stealing oldest; Weapons/*/{Impact,Detonate}
// and World/Asteroid/Hit 6 stealing quietest; Ships/Engine 12 stealing quietest; World/Ambience and
// Music/Game 1.
// HUD events are left alone. Idempotent: an event is routed only while it still outputs to bus:/World
// and capped only while it still has the default cap; every bus, effect, modulator, scope and send is
// created only when absent.
//
// Lives outside audio/fmod/Scripts/: Studio evaluates every script there on each project load.
// Run only while FMOD Studio is closed: the CLI discards the open GUI's unsaved-changes backup.
// fmodstudiocl -script audio/tools/scaffold_mix.js audio/fmod/Astronomical.fspro

var project = studio.project;
var mixer = project.workspace.mixer;

// Studio's VoiceStealingBehavior enum order; the macros drawer lists the choices in another order.
var stealing = { oldest: 0, quietest: 1 };
var defaultMaxInstances = 65;
var lowPass12dB = 1;
var maxDistance = 115;
// The zoomed-out screen edge is about 30 plane units out, so on-screen action stays unfiltered.
var distanceCutoffs = [[0, 22000], [25, 22000], [50, 3000], [80, 1200], [maxDistance, 500]];

function required(path) {
    var found = project.lookup(path);
    if (!found) throw new Error(path + " is missing");
    return found;
}

var world = required("bus:/World");
var music = required("bus:/Music");
var reverb = required("bus:/Reverb");
var lowHealth = required("snapshot:/LowHealth");

function subBus(name) {
    var path = "bus:/World/" + name;
    var existing = project.lookup(path);
    if (existing) return existing;
    var created = project.create("MixerGroup");
    created.name = name;
    created.output = world;
    console.log("created " + path);
    return created;
}

function routeFromWorld(event, target) {
    if (event.mixerInput.output.id !== world.id) return;
    event.mixerInput.output = target;
    console.log("routed " + event.getPath() + " to " + target.getPath());
}

function cap(event, maxInstances, steal) {
    var master = event.masterTrack.mixerGroup;
    if (master.maxInstances !== defaultMaxInstances || master.instanceStealing !== stealing.oldest) return;
    master.maxInstances = maxInstances;
    master.instanceStealing = steal;
    console.log("capped " + event.getPath() + " at " + maxInstances + " instances, stealing " + (steal === stealing.oldest ? "oldest" : "quietest"));
}

function existingEffect(bus, entity, matches) {
    return bus.effectChain.effects.filter(function (effect) {
        return effect.entity === entity && (!matches || matches(effect));
    })[0];
}

function faderIndex(bus) {
    var effects = bus.effectChain.effects;
    for (var i = 0; i < effects.length; i++) if (effects[i].entity === "MixerBusFader") return i;
    throw new Error(bus.getPath() + " has no fader");
}

function addPreFader(bus, entity) {
    var effect = project.create(entity);
    bus.effectChain.relationships.effects.insert(faderIndex(bus), effect);
    return effect;
}

function addPostFader(bus, entity) {
    return bus.effectChain.addEffect(entity);
}

function existingModulator(owner, entity, propertyName) {
    return owner.modulators.filter(function (modulator) {
        return modulator.entity === entity && modulator.nameOfPropertyBeingModulated === propertyName;
    })[0];
}

function scope(target, propertyName, value, label) {
    var scoped = lowHealth.snapshotProperties.some(function (property) {
        return property.automatableObject.id === target.id && property.propertyName === propertyName;
    });
    if (scoped) return;
    var property = project.create("SnapshotProperty");
    property.propertyName = propertyName;
    property.value = value;
    property.snapshot = lowHealth;
    property.automatableObject = target;
    console.log("scoped " + label + " into snapshot:/LowHealth at " + value);
}

function ensureSnapshotTrack(strip) {
    var present = lowHealth.snapshotTracks.some(function (track) { return track.mixerStrip.id === strip.id; });
    if (present) return;
    var track = project.create("SnapshotTrack");
    track.snapshot = lowHealth;
    track.mixerStrip = strip;
}

function isSpatial(event) {
    return !!existingEffect(event.masterTrack.mixerGroup, "SpatialiserEffect");
}

// Reuses the project-wide preset so GUIDs.txt never lists a second parameter of the same name.
function ensureDistanceParameter(event) {
    var present = event.getParameterPresets().some(function (parameter) {
        return parameter.presetOwner.name === "Distance";
    });
    if (present) return;
    var preset = project.lookup("parameter:/Distance");
    if (preset) event.addGameParameter(preset);
    else event.addGameParameter({ name: "Distance", type: project.parameterType.Distance, min: 0, max: maxDistance });
}

function distanceLowPass() {
    var existing = project.model.EffectPreset.findInstances().filter(function (preset) {
        return preset.name === "Distance Low-Pass";
    })[0];
    if (existing) return existing;
    var preset = project.create("EffectPreset");
    preset.name = "Distance Low-Pass";
    preset.folder = project.workspace.masterEffectPresetFolder;
    var eq = project.create("MultibandEqEffect");
    preset.effect = eq;
    eq.filterTypeA = lowPass12dB;
    eq.frequencyA = 22000;
    var curve = eq.addAutomator("frequencyA").addAutomationCurve(required("parameter:/Distance").parameter);
    distanceCutoffs.forEach(function (point) { curve.addAutomationPoint(point[0], point[1]); });
    console.log("created the shared effect Distance Low-Pass, cutoffs " + JSON.stringify(distanceCutoffs));
    return preset;
}

function ensureDistanceLowPass(event, preset) {
    var master = event.masterTrack.mixerGroup;
    var present = existingEffect(master, "ProxyEffect", function (proxy) { return proxy.preset && proxy.preset.id === preset.id; });
    if (present) return;
    addPreFader(master, "ProxyEffect").preset = preset;
    console.log("added Distance Low-Pass before the " + event.getPath() + " fader");
}

function ensureReverbSend(bus) {
    var present = existingEffect(bus, "MixerSend", function (send) { return send.mixerReturn && send.mixerReturn.id === reverb.id; });
    if (present) return;
    var send = addPostFader(bus, "MixerSend");
    send.mixerReturn = reverb;
    send.level = -18;
    console.log("added a -18 dB send to bus:/Reverb after the " + bus.getPath() + " fader");
}

var weapons = subBus("Weapons");
var ships = subBus("Ships");
var environment = subBus("Environment");

var weaponFire = new RegExp("^event:/Weapons/[^/]+/Fire$");
var weaponHit = new RegExp("^event:/Weapons/[^/]+/(Impact|Detonate)$");
project.model.Event.findInstances().forEach(function (event) {
    var path = event.getPath();
    if (path.indexOf("event:/Weapons/") === 0) routeFromWorld(event, weapons);
    else if (path.indexOf("event:/Ships/") === 0) routeFromWorld(event, ships);
    else if (path.indexOf("event:/World/") === 0) routeFromWorld(event, environment);

    if (weaponFire.test(path)) cap(event, 8, stealing.oldest);
    else if (weaponHit.test(path) || path === "event:/World/Asteroid/Hit") cap(event, 6, stealing.quietest);
    else if (path === "event:/Ships/Engine") cap(event, 12, stealing.quietest);
    else if (path === "event:/World/Ambience" || path === "event:/Music/Game") cap(event, 1, stealing.oldest);
});

var spatialEvents = project.model.Event.findInstances().filter(isSpatial);
spatialEvents.forEach(ensureDistanceParameter);
var lowPass = distanceLowPass();
spatialEvents.forEach(function (event) { ensureDistanceLowPass(event, lowPass); });

var masterBus = mixer.masterBus;
if (!existingEffect(masterBus, "LimiterEffect")) {
    addPostFader(masterBus, "LimiterEffect").ceiling = -1;
    console.log("added a Limiter with a -1 dB ceiling after the Master Bus fader");
}
if (!existingEffect(masterBus, "LoudnessMeter")) {
    addPostFader(masterBus, "LoudnessMeter");
    console.log("added a Loudness Meter after the Master Bus fader");
}

if (!existingEffect(world, "CompressorEffect")) {
    var compressor = addPreFader(world, "CompressorEffect");
    compressor.ratio = 2;
    compressor.attackTime = 100;
    compressor.gain = 0;
    console.log("added a 2:1 Compressor with a 100 ms attack before the bus:/World fader");
}
var worldEq = existingEffect(world, "MultibandEqEffect");
if (!worldEq) {
    worldEq = addPreFader(world, "MultibandEqEffect");
    worldEq.filterTypeA = lowPass12dB;
    worldEq.frequencyA = 22000;
    console.log("added a Multiband EQ, band A a low-pass parked at 22 kHz, before the bus:/World fader");
}

if (!existingEffect(music, "ThreeEQEffect")) {
    addPreFader(music, "ThreeEQEffect");
    console.log("added a flat 3-EQ before the bus:/Music fader");
}
var weaponsSidechain = existingEffect(weapons, "Sidechain");
if (!weaponsSidechain) {
    weaponsSidechain = addPostFader(weapons, "Sidechain");
    console.log("added a Sidechain after the bus:/World/Weapons fader");
}
if (!existingModulator(music, "SidechainModulator", "volume")) {
    var duck = music.addModulator("SidechainModulator", "volume");
    duck.amount = 4;
    duck.releaseTime = 1000;
    duck.relationships.sidechains.add(weaponsSidechain);
    console.log("added a sidechain modulator on bus:/Music volume: 4 dB from the Weapons sidechain, 1 s release");
}

scope(world, "volume", -10, "bus:/World volume");
scope(worldEq, "frequencyA", 1500, "the bus:/World Multiband EQ band A frequency");
ensureSnapshotTrack(world);
if (!existingModulator(lowHealth.automatableProperties, "ADSRModulator", "snapshotIntensity")) {
    var envelope = lowHealth.automatableProperties.addModulator("ADSRModulator", "snapshotIntensity");
    envelope.attackTime = 500;
    envelope.holdTime = 0;
    envelope.decayTime = 0;
    envelope.releaseTime = 500;
    envelope.peakValue = 100;
    envelope.sustainValue = 100;
    console.log("added an AHDSR on snapshot:/LowHealth intensity, 0.5 s attack and release");
}

ensureReverbSend(weapons);
ensureReverbSend(environment);

if (!project.save()) throw new Error("project save failed");
console.log("saved " + project.filePath);
