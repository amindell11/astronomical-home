// Pre-creates the keystone-audio contract KeystoneAudio consumes: one 3D event per kit in bank:/Ships,
// routed to bus:/World/Ships, each with the owner's clip from Assets/Sfx/Keystones/, imported in place.
//   event:/Ships/Keystones/Dash       Junker, Vanguard  boost.wav       plays through
//   event:/Ships/Keystones/Ram        Bulwark           ram_boost.wav   plays through
//   event:/Ships/Keystones/Blink      Crimson           blink.wav       plays through
//   event:/Ships/Keystones/Overdrive  Valis             overdrive.wav   held for the kit's window
//   event:/Ships/Keystones/Stealth    Nightshade        stealth_2.wav   held for the kit's window
// KeystoneAudio starts the event at each activation and keys it off when the kit's active window ends.
// A plays-through event has no sustain point, so the key-off changes nothing. A held event plays its
// clip async from a short cut-off region with a sustain point inside, as Missile/Flight does: the clip
// runs while the cursor holds, and the key-off moves the cursor out of the region, which cuts the clip
// through its AHDSR release.
// event:/Ships/Boost, the shared placeholder every kit played before, moves here as Dash and keeps its
// GUID; its laserBolt.wav placeholder gives way to boost.wav.
// Run scaffold_mix.js afterwards so the new 3D events get the Distance parameter and Low-Pass.
// Idempotent: an event already present is left as the designer has it.
//
// Lives outside audio/fmod/Scripts/: Studio evaluates every script there on each project load.
// Run only while FMOD Studio is closed: the CLI discards the open GUI's unsaved-changes backup.
// fmodstudiocl -script audio/tools/scaffold_keystones.js audio/fmod/Astronomical.fspro

var project = studio.project;
var workspace = project.workspace;
var assetDir = __dirname + "/../fmod/Assets/";
var maxDistance = 115;
var holdSeconds = 0.05;
var sustainLeadSeconds = 0.01;
var releaseMilliseconds = 400;

function required(path) {
    var found = project.lookup(path);
    if (!found) throw new Error(path + " is missing");
    return found;
}

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

function clip(assetPath) {
    var existing = project.model.AudioFile.findInstances().filter(function (asset) {
        return asset.assetPath === assetPath;
    })[0];
    if (existing) return existing;
    var imported = project.importAudioFile(assetDir + assetPath);
    if (!imported) throw new Error("could not import " + assetDir + assetPath);
    console.log("imported " + imported.assetPath + " (" + imported.length + "s)");
    return imported;
}

var shipsBank = required("bank:/Ships");
var shipsBus = required("bus:/World/Ships");
var keystones = childFolder(childFolder(workspace.masterEventFolder, "Ships"), "Keystones");

function kitEvent(name) {
    var existing = project.lookup("event:/Ships/Keystones/" + name);
    if (existing) return existing;
    var created = workspace.addEvent(name, true);
    created.folder = keystones;
    created.automatableProperties.maximumDistance = maxDistance;
    created.mixerInput.output = shipsBus;
    created.relationships.banks.add(shipsBank);
    console.log("created " + created.getPath() + " in bank:/Ships, routed to bus:/World/Ships");
    return created;
}

function dashFromBoost() {
    var existing = project.lookup("event:/Ships/Keystones/Dash");
    if (existing) return;
    var boost = required("event:/Ships/Boost");
    boost.folder = keystones;
    boost.name = "Dash";
    console.log("moved event:/Ships/Boost to " + boost.getPath());
    var sounds = boost.groupTracks[0].modules;
    if (sounds.length !== 1 || sounds[0].audioFile.assetPath !== "laserBolt.wav") return;
    var audio = clip("Sfx/Keystones/boost.wav");
    sounds[0].audioFile = audio;
    sounds[0].length = audio.length;
    console.log("swapped the laserBolt.wav placeholder for " + audio.assetPath);
}

function ensurePlaysThrough(event, assetPath) {
    if (event.groupTracks.length > 0) return;
    var audio = clip(assetPath);
    event.addGroupTrack(event.name).addSound(event.timeline, "SingleSound", 0, audio.length).audioFile = audio;
    console.log("placed " + audio.assetPath + " on " + event.getPath());
}

function ensureHeld(event, assetPath) {
    if (event.groupTracks.length > 0) return;
    var audio = clip(assetPath);
    var sound = event.addGroupTrack(event.name).addSound(event.timeline, "SingleSound", 0, holdSeconds);
    sound.audioFile = audio;
    sound.isAsync = true;
    sound.isCutoff = true;
    var envelope = sound.addModulator("ADSRModulator", "volume");
    envelope.initialValue = 0;
    envelope.attackTime = 0;
    envelope.releaseTime = releaseMilliseconds;
    envelope.finalValue = -80;
    var markers = event.markerTracks.length > 0 ? event.markerTracks[0] : event.addMarkerTrack();
    markers.addSustainPoint(holdSeconds - sustainLeadSeconds);
    console.log("placed " + audio.assetPath + " held at a sustain point on " + event.getPath());
}

dashFromBoost();
ensurePlaysThrough(kitEvent("Ram"), "Sfx/Keystones/ram_boost.wav");
ensurePlaysThrough(kitEvent("Blink"), "Sfx/Keystones/blink.wav");
ensureHeld(kitEvent("Overdrive"), "Sfx/Keystones/overdrive.wav");
ensureHeld(kitEvent("Stealth"), "Sfx/Keystones/stealth_2.wav");

if (!project.save()) throw new Error("project save failed");
console.log("saved " + project.filePath);
