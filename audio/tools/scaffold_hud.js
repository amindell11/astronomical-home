// Pre-creates the HUD-audio contract the game code consumes, and a starting bus tree:
//   bus:/World   every event:/Weapons/* and event:/Ships/* event still routed to the master bus
//   bus:/HUD     every event:/HUD/* event
//   bus:/Music   empty until music ports
//   snapshot:/LowHealth   empty; the designer scopes the world duck and places it in HUD/LowHealth.
//   Snapshots build into the master bank, and assigning one to a bank crashes the CLI.
// HUD events, all 2D, in bank:/HUD:
//   event:/HUD/LowHealth           looping, held by UIHealthAudio
//   event:/HUD/LockOn/Locking      looping, held by UILockOnAudio
//   event:/HUD/LockOn/Locked       one-shot
//   event:/HUD/Overheat            one-shot
//   event:/HUD/BoostReady          one-shot
//   event:/HUD/ShieldDepleted      one-shot
//   event:/HUD/IncomingMissile     looping, held by UIMissileWarningAudio
// Placeholders are the designer's clips under Assets/Sfx/, imported in place, because an empty
// event stops on its first update. Idempotent: anything already present is left as the designer
// has it, and an event already routed to a bus other than the master stays there.
//
// Lives outside audio/fmod/Scripts/: Studio evaluates every script there on each project load.
// Run only while FMOD Studio is closed: the CLI discards the open GUI's unsaved-changes backup.
// fmodstudiocl -script audio/tools/scaffold_hud.js audio/fmod/Astronomical.fspro

var project = studio.project;
var workspace = project.workspace;
var mixer = workspace.mixer;
var assetDir = __dirname + "/../fmod/Assets/";

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

function bus(name) {
    var existing = project.lookup("bus:/" + name);
    if (existing) return existing;
    var created = project.create("MixerGroup");
    created.name = name;
    created.output = mixer.masterBus;
    console.log("created bus:/" + name);
    return created;
}

function snapshot(name) {
    var existing = project.lookup("snapshot:/" + name);
    if (existing) return existing;
    var created = project.create("Snapshot");
    created.name = name;
    created.folder = mixer.snapshotList;
    console.log("created snapshot:/" + name);
    return created;
}

function event(folder, path, name) {
    var existing = project.lookup(path);
    if (existing) return existing;
    var created = workspace.addEvent(name, false);
    created.folder = folder;
    console.log("created " + created.getPath());
    return created;
}

function ensureBank(event, bank) {
    if (event.banks.some(function (assigned) { return assigned.id === bank.id; })) return;
    event.relationships.banks.add(bank);
    console.log("assigned " + event.getPath() + " to bank:/" + bank.name);
}

function routeFromMaster(event, target) {
    if (event.mixerInput.output.id !== mixer.masterBus.id) return;
    event.mixerInput.output = target;
    console.log("routed " + event.getPath() + " to bus:/" + target.name);
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

function placeSound(event, audio) {
    var track = event.addGroupTrack("Cue");
    var sound = track.addSound(event.timeline, "SingleSound", 0, audio.length);
    sound.audioFile = audio;
    return sound;
}

function ensureOneShotPlaceholder(event, assetPath) {
    if (event.groupTracks.length > 0) return;
    var audio = clip(assetPath);
    placeSound(event, audio);
    console.log("placed " + audio.assetPath + " on " + event.getPath());
}

function ensureLoopPlaceholder(event, assetPath) {
    if (event.groupTracks.length > 0) return;
    var audio = clip(assetPath);
    placeSound(event, audio).looping = true;
    var region = project.create("LoopRegion");
    region.position = 0;
    region.length = audio.length;
    region.timeline = event.timeline;
    region.markerTrack = event.markerTracks.length > 0 ? event.markerTracks[0] : event.addMarkerTrack();
    console.log("placed looping " + audio.assetPath + " in a loop region on " + event.getPath());
}

var world = bus("World");
var hud = bus("HUD");
bus("Music");

var hudBank = bank("HUD");
snapshot("LowHealth");

var hudFolder = childFolder(workspace.masterEventFolder, "HUD");
var lockOnFolder = childFolder(hudFolder, "LockOn");

var loops = [
    [event(hudFolder, "event:/HUD/LowHealth", "LowHealth"), "Sfx/UI/low_hp_alert.wav"],
    [event(lockOnFolder, "event:/HUD/LockOn/Locking", "Locking"), "Sfx/UI/missile_locking.wav"],
    [event(hudFolder, "event:/HUD/IncomingMissile", "IncomingMissile"), "Sfx/Scratch/alert_thing.wav"]
];
var oneShots = [
    [event(lockOnFolder, "event:/HUD/LockOn/Locked", "Locked"), "Sfx/UI/missile_locked.wav"],
    [event(hudFolder, "event:/HUD/Overheat", "Overheat"), "Sfx/UI/overheat.wav"],
    [event(hudFolder, "event:/HUD/BoostReady", "BoostReady"), "Sfx/UI/boostReady.wav"],
    [event(hudFolder, "event:/HUD/ShieldDepleted", "ShieldDepleted"), "Sfx/UI/shield_depleted.wav"]
];

loops.forEach(function (each) { ensureLoopPlaceholder(each[0], each[1]); });
oneShots.forEach(function (each) { ensureOneShotPlaceholder(each[0], each[1]); });
loops.concat(oneShots).forEach(function (each) {
    ensureBank(each[0], hudBank);
    routeFromMaster(each[0], hud);
});

project.model.Event.findInstances().forEach(function (each) {
    var path = each.getPath();
    if (path.indexOf("event:/Weapons/") === 0 || path.indexOf("event:/Ships/") === 0)
        routeFromMaster(each, world);
});

if (!project.save()) throw new Error("project save failed");
console.log("saved " + project.filePath);
