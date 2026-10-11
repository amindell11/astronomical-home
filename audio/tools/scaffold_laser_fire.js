// Pre-creates the laser-fire contract the game code consumes: event:/Weapons/Laser/Fire with a
// continuous Heat parameter (0-1, the weapon's heat fraction after the shot), assigned to the
// Weapons bank, with laserBolt.wav placed as a starting instrument.
// Idempotent: anything already present is left as the designer has it.
//
// Lives outside audio/fmod/Scripts/: Studio evaluates every script there on each project load.
// fmodstudiocl -script audio/tools/scaffold_laser_fire.js audio/fmod/Astronomical.fspro

var project = studio.project;
var workspace = project.workspace;
var starterClip = __dirname + "/../../src/Asteroids3D/Assets/Audio/Sfx/Fx/laserBolt.wav";

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

function fireEvent(folder) {
    var existing = project.lookup("event:/Weapons/Laser/Fire");
    if (existing) return existing;
    var created = workspace.addEvent("Fire", true);
    created.folder = folder;
    console.log("created " + created.getPath());
    return created;
}

function ensureHeat(event) {
    var present = event.getParameterPresets().some(function (parameter) {
        return parameter.presetOwner.name === "Heat";
    });
    if (present) return;
    event.addGameParameter({ name: "Heat", type: project.parameterType.User, min: 0, max: 1 });
    console.log("created parameter Heat");
}

function ensureStarterInstrument(event) {
    if (event.groupTracks.length > 0) return;
    var clip = project.importAudioFile(starterClip);
    if (!clip) throw new Error("could not import " + starterClip);
    var track = event.addGroupTrack("Fire");
    var sound = track.addSound(event.timeline, "SingleSound", 0, clip.length);
    sound.audioFile = clip;
    console.log("placed " + clip.assetPath + " (" + clip.length + "s)");
}

var laser = childFolder(childFolder(workspace.masterEventFolder, "Weapons"), "Laser");
var event = fireEvent(laser);
ensureHeat(event);
var weapons = bank("Weapons");
if (!event.banks.some(function (assigned) { return assigned.id === weapons.id; })) {
    event.relationships.banks.add(weapons);
    console.log("assigned to bank:/Weapons");
}
ensureStarterInstrument(event);

if (!project.save()) throw new Error("project save failed");
console.log("saved " + project.filePath);
