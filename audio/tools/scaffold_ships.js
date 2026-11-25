// Pre-creates the ship-audio contract the game code consumes, all in bank:/Ships:
//   event:/Ships/Engine  (Thrust in [-1, 1], Strafe in [0, 1])  looping, held by EngineAudio
//   event:/Ships/Boost                                          one-shot per boost
// Both are 3D with the weapon events' max distance of 115 plane units.
// Each event gets a placeholder instrument from a clip the project already imports, because an
// empty event stops on its first update. Idempotent: anything already present is left as the
// designer has it.
//
// Lives outside audio/fmod/Scripts/: Studio evaluates every script there on each project load.
// Run only while FMOD Studio is closed: the CLI discards the open GUI's unsaved-changes backup.
// fmodstudiocl -script audio/tools/scaffold_ships.js audio/fmod/Astronomical.fspro

var project = studio.project;
var workspace = project.workspace;
var maxDistance = 115;

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

function event(folder, name) {
    var path = "event:/Ships/" + name;
    var existing = project.lookup(path);
    if (existing) return existing;
    var created = workspace.addEvent(name, true);
    created.folder = folder;
    created.automatableProperties.maximumDistance = maxDistance;
    console.log("created " + created.getPath());
    return created;
}

function ensureBank(event, bank) {
    if (event.banks.some(function (assigned) { return assigned.id === bank.id; })) return;
    event.relationships.banks.add(bank);
    console.log("assigned " + event.getPath() + " to bank:/" + bank.name);
}

// Reuses the project-wide preset so GUIDs.txt never lists a second parameter of the same name.
function ensureParameter(event, name, min, max) {
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
    event.addGameParameter({ name: name, type: project.parameterType.User, min: min, max: max });
    console.log("created parameter " + name + " [" + min + ", " + max + "] on " + event.getPath());
}

function importedClip(fileName) {
    var clip = project.model.AudioFile.findInstances().filter(function (asset) {
        return asset.assetPath === fileName;
    })[0];
    if (!clip) throw new Error("placeholder clip " + fileName + " is not imported in the project");
    return clip;
}

function placeSound(event, trackName, clip) {
    var track = event.addGroupTrack(trackName);
    var sound = track.addSound(event.timeline, "SingleSound", 0, clip.length);
    sound.audioFile = clip;
    return sound;
}

function ensureLoopPlaceholder(event, fileName) {
    if (event.groupTracks.length > 0) return;
    var clip = importedClip(fileName);
    placeSound(event, "Engine", clip).looping = true;
    var region = project.create("LoopRegion");
    region.position = 0;
    region.length = clip.length;
    region.timeline = event.timeline;
    region.markerTrack = event.markerTracks.length > 0 ? event.markerTracks[0] : event.addMarkerTrack();
    console.log("placed looping " + clip.assetPath + " in a loop region on " + event.getPath());
}

function ensureOneShotPlaceholder(event, fileName) {
    if (event.groupTracks.length > 0) return;
    var clip = importedClip(fileName);
    placeSound(event, "Boost", clip);
    console.log("placed " + clip.assetPath + " on " + event.getPath());
}

var shipsFolder = childFolder(workspace.masterEventFolder, "Ships");
var shipsBank = bank("Ships");

var engine = event(shipsFolder, "Engine");
ensureParameter(engine, "Thrust", -1, 1);
ensureParameter(engine, "Strafe", 0, 1);
ensureLoopPlaceholder(engine, "engineRumbleLow.wav");

var boost = event(shipsFolder, "Boost");
ensureOneShotPlaceholder(boost, "laserBolt.wav");

[engine, boost].forEach(function (each) {
    ensureBank(each, shipsBank);
});

if (!project.save()) throw new Error("project save failed");
console.log("saved " + project.filePath);
