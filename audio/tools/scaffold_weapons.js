// Pre-creates the weapon-audio contract the game code consumes, all in bank:/Weapons:
//   event:/Weapons/TurboLaser/Fire   (Heat)       one-shot
//   event:/Weapons/ChargeLaser/Fire  (Charge)     sustain point, keyed off on the shot
//   event:/Weapons/Railgun/Fire      (Charge)     sustain point, keyed off on the shot
//   event:/Weapons/Railgun/Impact                 one-shot
//   event:/Weapons/Ripper/Fire       (FirstShot)  one-shot
//   event:/Weapons/Grenade/Fire                   one-shot
//   event:/Weapons/Grenade/Detonate               one-shot
//   event:/Weapons/Missile/Flight    (Speed)      sustain point, stopped with fade-out, never keyed off
//   event:/Weapons/Missile/Detonate               one-shot
// Heat is the existing preset; Charge is one preset in [0, 1] shared by both charge events.
// FirstShot is discrete in [0, 1]: the game sends 1 on the first shot after any firing gap.
// Speed is a preset in [0, 1]: the missile's speed as a fraction of its homing speed.
// Every event gets a placeholder instrument from the tracked Unity clips, because an empty event
// stops on its first update. Idempotent: anything already present is left as the designer has it.
//
// Charge events: a looping placeholder spans the first 0.25 s of the timeline, the sustain point
// sits just before its end and the release placeholder starts right after it, so a key-off reaches
// the release at once. Key-offs are reference-counted, so a sustain point at the end of the 10 s
// rumble clip would hold the release back by the rest of the loop; one at zero would play the
// whole loop before the release.
//
// Missile flight: the launch clip and a looping thruster start at zero, both async, so they keep
// playing while the cursor holds at a sustain point inside the thruster's short region. The game
// never keys it off; a fade-out stop ends it through the thruster's AHDSR release. Doppler is on
// for the velocity FMOD reads from the missile's Rigidbody.
//
// Lives outside audio/fmod/Scripts/: Studio evaluates every script there on each project load.
// Run only while FMOD Studio is closed: the CLI discards the open GUI's unsaved-changes backup.
// fmodstudiocl -script audio/tools/scaffold_weapons.js audio/fmod/Astronomical.fspro

var project = studio.project;
var workspace = project.workspace;
var clipDir = __dirname + "/../../src/Asteroids3D/Assets/Audio/Sfx/Fx/";
var chargeLoopSeconds = 0.25;
var sustainLeadSeconds = 0.01;
var flightHoldSeconds = 0.05;

function childFolder(parent, name) {
    var existing = parent.items.filter(function (item) {
        return item.isOfExactType("EventFolder") && item.name === name;
    })[0];
    if (existing) return existing;
    var folder = project.create("EventFolder");
    folder.name = name;
    folder.folder = parent;
    console.log("created folder " + name);
    return folder;
}

function bank(name) {
    var existing = project.lookup("bank:/" + name);
    if (existing) return existing;
    var created = project.create("Bank");
    created.name = name;
    created.folder = workspace.masterBankFolder;
    console.log("created bank:/" + name);
    return created;
}

function event(folder, family, name) {
    var path = "event:/Weapons/" + family + "/" + name;
    var existing = project.lookup(path);
    if (existing) return existing;
    var created = workspace.addEvent(name, true);
    created.folder = folder;
    console.log("created " + created.getPath());
    return created;
}

function ensureBank(event, bank) {
    if (event.banks.some(function (assigned) { return assigned.id === bank.id; })) return;
    event.relationships.banks.add(bank);
    console.log("assigned " + event.getPath() + " to bank:/" + bank.name);
}

// Reuses the project-wide preset so GUIDs.txt never lists a second parameter of the same name.
function ensureParameter(event, name, type) {
    var present = event.getParameterPresets().some(function (parameter) {
        return parameter.presetOwner.name === name;
    });
    if (present) return;
    var preset = project.lookup("parameter:/" + name);
    if (preset) {
        event.addGameParameter(preset);
        console.log("added existing parameter " + name + " to " + event.getPath());
        return;
    }
    event.addGameParameter({ name: name, type: type, min: 0, max: 1 });
    console.log("created parameter " + name + " on " + event.getPath());
}

function audioFile(fileName) {
    var existing = project.model.AudioFile.findInstances().filter(function (asset) {
        return asset.assetPath === fileName;
    })[0];
    if (existing) return existing;
    var imported = project.importAudioFile(clipDir + fileName);
    if (!imported) throw new Error("could not import " + clipDir + fileName);
    console.log("imported " + imported.assetPath + " (" + imported.length + "s)");
    return imported;
}

function placeSound(event, trackName, clip, start, length) {
    var track = event.addGroupTrack(trackName);
    var sound = track.addSound(event.timeline, "SingleSound", start, length);
    sound.audioFile = clip;
    return sound;
}

function ensureOneShotPlaceholder(event, fileName) {
    if (event.groupTracks.length > 0) return;
    var clip = audioFile(fileName);
    placeSound(event, "Fire", clip, 0, clip.length);
    console.log("placed " + clip.assetPath + " on " + event.getPath());
}

function ensureChargePlaceholder(event) {
    if (event.groupTracks.length > 0) return;
    var loop = placeSound(event, "Charge", audioFile("engineRumbleLow.wav"), 0, chargeLoopSeconds);
    loop.looping = true;
    var release = audioFile("laserBolt.wav");
    placeSound(event, "Release", release, chargeLoopSeconds, release.length);
    var markers = event.markerTracks.length > 0 ? event.markerTracks[0] : event.addMarkerTrack();
    markers.addSustainPoint(chargeLoopSeconds - sustainLeadSeconds);
    console.log("placed charge loop, sustain point and release on " + event.getPath());
}

function ensureFlightPlaceholder(event) {
    if (event.groupTracks.length > 0) return;
    var launchClip = audioFile("laserBolt.wav");
    var launch = placeSound(event, "Launch", launchClip, 0, launchClip.length);
    launch.isAsync = true;
    var thruster = placeSound(event, "Thruster", audioFile("engineRumbleLow.wav"), 0, flightHoldSeconds);
    thruster.isAsync = true;
    thruster.isCutoff = true;
    thruster.looping = true;
    var envelope = thruster.addModulator("ADSRModulator", "volume");
    envelope.initialValue = -80;
    envelope.attackTime = 250;
    envelope.releaseTime = 150;
    envelope.finalValue = -80;
    var markers = event.markerTracks.length > 0 ? event.markerTracks[0] : event.addMarkerTrack();
    markers.addSustainPoint(flightHoldSeconds - sustainLeadSeconds);
    event.automatableProperties.dopplerEnabled = true;
    console.log("placed launch, held thruster loop and sustain point on " + event.getPath());
}

var weaponsFolder = childFolder(workspace.masterEventFolder, "Weapons");
var weaponsBank = bank("Weapons");

var turboLaser = event(childFolder(weaponsFolder, "TurboLaser"), "TurboLaser", "Fire");
ensureParameter(turboLaser, "Heat", project.parameterType.User);
ensureOneShotPlaceholder(turboLaser, "laserBolt.wav");

var chargeLaser = event(childFolder(weaponsFolder, "ChargeLaser"), "ChargeLaser", "Fire");
ensureParameter(chargeLaser, "Charge", project.parameterType.User);
ensureChargePlaceholder(chargeLaser);

var railgunFolder = childFolder(weaponsFolder, "Railgun");
var railgun = event(railgunFolder, "Railgun", "Fire");
ensureParameter(railgun, "Charge", project.parameterType.User);
ensureChargePlaceholder(railgun);
var railgunImpact = event(railgunFolder, "Railgun", "Impact");
ensureOneShotPlaceholder(railgunImpact, "laserImpact1.wav");

var ripper = event(childFolder(weaponsFolder, "Ripper"), "Ripper", "Fire");
ensureParameter(ripper, "FirstShot", project.parameterType.UserDiscrete);
ensureOneShotPlaceholder(ripper, "laserBolt.wav");

var grenadeFolder = childFolder(weaponsFolder, "Grenade");
var grenade = event(grenadeFolder, "Grenade", "Fire");
ensureOneShotPlaceholder(grenade, "laserBolt.wav");
var detonate = event(grenadeFolder, "Grenade", "Detonate");
ensureOneShotPlaceholder(detonate, "rockExplosion1.wav");

var missileFolder = childFolder(weaponsFolder, "Missile");
var missileFlight = event(missileFolder, "Missile", "Flight");
ensureParameter(missileFlight, "Speed", project.parameterType.User);
ensureFlightPlaceholder(missileFlight);
var missileDetonate = event(missileFolder, "Missile", "Detonate");
ensureOneShotPlaceholder(missileDetonate, "rockExplosion1.wav");

[turboLaser, chargeLaser, railgun, railgunImpact, ripper, grenade, detonate, missileFlight, missileDetonate].forEach(function (each) {
    ensureBank(each, weaponsBank);
});

if (!project.save()) throw new Error("project save failed");
console.log("saved " + project.filePath);
