// Pre-creates the impact, damage and world-audio contract the game code consumes, all routed to
// bus:/World (scaffold_hud.js creates it):
//   bank:/Weapons  event:/Weapons/{Laser,TurboLaser,ChargeLaser,Ripper}/Impact   3D one-shots
//   bank:/Ships    event:/Ships/{ShieldHit,HullHit,Death,CollisionDeath}          3D one-shots
//   bank:/World    event:/World/Asteroid/{Hit,Break}                              3D one-shots
//                  event:/World/Ambience                                          2D loop
// 3D events take the weapon events' max distance of 115 plane units. Placeholders are clips under
// Assets/, imported in place when the project lacks them, because an empty event stops on its first
// update. An event given several clips plays one per trigger, with pitch and volume randomized.
// Idempotent: anything already present is left as the designer has it.
//
// Lives outside audio/fmod/Scripts/: Studio evaluates every script there on each project load.
// Run only while FMOD Studio is closed: the CLI discards the open GUI's unsaved-changes backup.
// fmodstudiocl -script audio/tools/scaffold_world.js audio/fmod/Astronomical.fspro

var project = studio.project;
var workspace = project.workspace;
var assetDir = __dirname + "/../fmod/Assets/";
var maxDistance = 115;
var pitchSpreadSemitones = 2;
var volumeSpreadDb = 3;

var world = project.lookup("bus:/World");
if (!world) throw new Error("bus:/World is missing: run scaffold_hud.js first");

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

function folderPath(names) {
    var folder = workspace.masterEventFolder;
    names.forEach(function (name) { folder = childFolder(folder, name); });
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

function addVariedSound(event, assetPaths) {
    var audios = assetPaths.map(clip);
    var length = Math.max.apply(null, audios.map(function (audio) { return audio.length; }));
    var multi = event.addGroupTrack("Cue").addSound(event.timeline, "MultiSound", 0, length);
    audios.forEach(function (audio) {
        var single = project.create("SingleSound");
        single.audioFile = audio;
        multi.relationships.sounds.add(single);
    });
    multi.addModulator("RandomizerModulator", "pitch").amount = pitchSpreadSemitones;
    multi.addModulator("RandomizerModulator", "volume").amount = volumeSpreadDb;
}

// Creation, placeholder, bank and routing all happen only for a new event, so a designed one is untouched.
function ensureEvent(path, spatial, looping, assetPath, assigned) {
    var existing = project.lookup(path);
    if (existing) return existing;
    var parts = path.replace("event:/", "").split("/");
    var name = parts.pop();
    var created = workspace.addEvent(name, spatial);
    created.folder = folderPath(parts);
    if (spatial) created.automatableProperties.maximumDistance = maxDistance;
    created.relationships.banks.add(assigned);
    created.mixerInput.output = world;

    if (Array.isArray(assetPath)) {
        addVariedSound(created, assetPath);
        console.log("created " + path + " with " + assetPath.join(", ") + " in bank:/" + assigned.name);
        return created;
    }
    var audio = clip(assetPath);
    var sound = created.addGroupTrack("Cue").addSound(created.timeline, "SingleSound", 0, audio.length);
    sound.audioFile = audio;
    if (looping) {
        sound.looping = true;
        var region = project.create("LoopRegion");
        region.position = 0;
        region.length = audio.length;
        region.timeline = created.timeline;
        region.markerTrack = created.markerTracks.length > 0 ? created.markerTracks[0] : created.addMarkerTrack();
    }
    console.log("created " + path + " with " + audio.assetPath + " in bank:/" + assigned.name);
    return created;
}

var weapons = bank("Weapons");
var ships = bank("Ships");
var worldBank = bank("World");

["Laser", "TurboLaser", "ChargeLaser", "Ripper"].forEach(function (weapon) {
    ensureEvent("event:/Weapons/" + weapon + "/Impact", true, false, "laserImpact1.wav", weapons);
});
ensureEvent("event:/Ships/ShieldHit", true, false, "Sfx/Impacts/shield_hit_1.wav", ships);
ensureEvent("event:/Ships/HullHit", true, false, "Sfx/Impacts/hull_thud_main.wav", ships);
ensureEvent("event:/Ships/Death", true, false, "Sfx/Explosions/ship_explosion.wav", ships);
ensureEvent("event:/Ships/CollisionDeath", true, false, "rockExplosion1.wav", ships);
ensureEvent("event:/World/Asteroid/Hit", true, false,
    ["Sfx/Impacts/rock_hit.wav", "Sfx/Impacts/rock_hit_2.wav", "Sfx/Impacts/rock_hit_3.wav"], worldBank);
ensureEvent("event:/World/Asteroid/Break", true, false, "Sfx/Explosions/asteroid_explosion.wav", worldBank);
ensureEvent("event:/World/Ambience", false, true, "Sfx/Ambience/spaceAmbience1.mp3", worldBank);

if (!project.save()) throw new Error("project save failed");
console.log("saved " + project.filePath);
