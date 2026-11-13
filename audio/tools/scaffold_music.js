// Pre-creates the music contract GameMusic consumes, in bank:/Music and routed to bus:/Music
// (scaffold_hud.js creates the bus):
//   event:/Music/Game   2D loop, one instance held for the session
//     MusicState        labelled: Hangar, Combat, Extract, Recap, Victory, Defeat, Elite
//     Phase             discrete, the survive phase's 0-based index
//     Locale            labelled, one label per locale prefab's name; the code sets it by label
// The placeholder is the owner's hangar track, imported in place, because an empty event stops on
// its first update. The owner builds the states and transitions in Studio. Idempotent: anything
// already present is left as the designer has it, except that labels missing from the end of
// MusicState are appended, so existing indices keep their meaning, and Elite gets a copy of
// Extract's cue in its own slot, so the owner can later give it a cue of its own.
//
// Lives outside audio/fmod/Scripts/: Studio evaluates every script there on each project load.
// Run only while FMOD Studio is closed: the CLI discards the open GUI's unsaved-changes backup.
// fmodstudiocl -script audio/tools/scaffold_music.js audio/fmod/Astronomical.fspro

var project = studio.project;
var workspace = project.workspace;
var assetDir = __dirname + "/../fmod/Assets/";
var placeholder = "Hero's_Landing_3.wav";
var states = ["Hangar", "Combat", "Extract", "Recap", "Victory", "Defeat", "Elite"];
var lastPhase = 7;
var locales = ["BlueCrescent", "CopperTurquoise", "CoralSunset", "CyanAurora", "IllustratedBlue", "VerdantGiant"];

var musicBus = project.lookup("bus:/Music");
if (!musicBus) throw new Error("bus:/Music is missing: run scaffold_hud.js first");

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

// Creation, placeholder, bank and routing all happen only for a new event, so a designed one is untouched.
function ensureLoopEvent(path, assetPath, assigned) {
    var existing = project.lookup(path);
    if (existing) return existing;
    var parts = path.replace("event:/", "").split("/");
    var name = parts.pop();
    var created = workspace.addEvent(name, false);
    var folder = workspace.masterEventFolder;
    parts.forEach(function (part) { folder = childFolder(folder, part); });
    created.folder = folder;

    var audio = clip(assetPath);
    var sound = created.addGroupTrack("Cue").addSound(created.timeline, "SingleSound", 0, audio.length);
    sound.audioFile = audio;
    sound.looping = true;
    var region = project.create("LoopRegion");
    region.position = 0;
    region.length = audio.length;
    region.timeline = created.timeline;
    region.markerTrack = created.markerTracks.length > 0 ? created.markerTracks[0] : created.addMarkerTrack();

    created.relationships.banks.add(assigned);
    created.mixerInput.output = musicBus;
    console.log("created " + path + " with " + audio.assetPath + " in bank:/" + assigned.name);
    return created;
}

// Reuses the project-wide preset so GUIDs.txt never lists a second parameter of the same name.
function ensureParameter(event, definition) {
    var present = event.getParameterPresets().some(function (parameter) {
        return parameter.presetOwner.name === definition.name;
    });
    if (present) return;
    var preset = project.lookup("parameter:/" + definition.name);
    if (preset) {
        event.addGameParameter(preset);
        console.log("added existing parameter " + definition.name + " to " + event.getPath());
        return;
    }
    event.addGameParameter(definition);
    console.log("created parameter " + definition.name + " on " + event.getPath());
}

// Appends only: a reordered or renamed label would move an index GameMusic.State already sends.
function ensureTrailingLabels(name, labels) {
    var parameter = project.lookup("parameter:/" + name).parameter;
    var present = parameter.enumerationLabels;
    for (var i = 0; i < present.length; i++) {
        if (present[i] !== labels[i])
            throw new Error(name + " label " + i + " is " + present[i] + ", expected " + labels[i]);
    }
    if (present.length === labels.length) return;
    parameter.enumerationLabels = labels;
    parameter.maximum = labels.length;
    console.log("appended " + labels.slice(present.length).join(", ") + " to " + name);
}

var envelope = ["initialValue", "attackTime", "attackShape", "holdTime", "peakValue", "decayTime", "decayShape",
    "sustainValue", "releaseTime", "releaseShape", "finalValue"];

// A copy, not a stretched range: a sheet instrument spans contiguous labels, and Elite is not next to Extract.
function ensureCopiedCue(event, parameterName, from, to) {
    var proxy = event.parameters.filter(function (p) { return p.preset.presetOwner.name === parameterName; })[0];
    function at(index) { return proxy.modules.filter(function (m) { return m.start === index; })[0]; }
    if (at(to)) return;
    var source = at(from);
    if (!source) {
        console.log("no " + parameterName + " cue at " + from + " yet; left " + to + " empty");
        return;
    }
    if (!source.isOfExactType("SingleSound"))
        throw new Error(parameterName + " " + from + " is a " + source.entity + ", not a single sound to copy");

    var copy = event.groupTracks[0].addSound(proxy, "SingleSound", to, source.length);
    copy.audioFile = source.audioFile;
    copy.startOffset = source.startOffset;
    copy.looping = source.looping;
    copy.isCutoff = source.isCutoff;
    copy.releaseTail = source.releaseTail;
    source.modulators.forEach(function (modulator) {
        var copied = copy.addModulator(modulator.entity, modulator.nameOfPropertyBeingModulated);
        envelope.forEach(function (property) {
            if (modulator[property] !== undefined) copied[property] = modulator[property];
        });
    });
    console.log("copied " + parameterName + " " + from + "'s " + source.audioFile.assetPath + " to " + to);
}

var game = ensureLoopEvent("event:/Music/Game", placeholder, bank("Music"));
ensureParameter(game, { name: "MusicState", type: project.parameterType.UserEnumeration, enumerationLabels: states });
ensureTrailingLabels("MusicState", states);
ensureCopiedCue(game, "MusicState", states.indexOf("Extract"), states.indexOf("Elite"));
ensureParameter(game, { name: "Phase", type: project.parameterType.UserDiscrete, min: 0, max: lastPhase });
ensureParameter(game, { name: "Locale", type: project.parameterType.UserEnumeration, enumerationLabels: locales });

if (!project.save()) throw new Error("project save failed");
console.log("saved " + project.filePath);
