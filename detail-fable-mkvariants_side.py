import json
OMIT = ["20 lower swept blades","21 lower blade lavender tips","21 lower blade lavender tips underside","22 lower blade dark inner facets","40 forward outriggers","41 outrigger lavender toes","42 outrigger inner sockets","51 midship auxiliary vanes","52 auxiliary vane feet","53 auxiliary vane roots","07 rear crest","50 aft split prongs"]
wing = {"top": "gray", "bevel": "ivory", "under": "ivory"}
V = {"rival": {}}
edge = {"top": "gray", "bevel": "lavender", "under": "ivory"}
ridge = {"top": "ivory", "bevel": "gray", "under": "graphite"}
edge = {"top": "gray", "bevel": "lavender", "under": "ivory"}
V["A_sabre_rails"] = {"omit": OMIT, "lofts": [{
  "sections": [[1.40,.66,.05,.31,.03,.03,.5,0],[0.3,.88,.18,.43,.06,.05,.5,5],[-1.1,.98,.30,.64,.09,.06,.5,10],[-2.2,1.12,.26,.88,.08,.06,.4,12],[-3.8,1.42,0,1.16,0,0,0,12]],
  "mats": [wing, wing, wing, "lavender"]}]}
V["B_tail_fangs"] = {"omit": OMIT, "lofts": [{
  "sections": [[0.9,.72,.06,.36,.03,.03,.3,0],[-0.2,.90,.24,.50,.10,.05,.3,8],[-1.2,1.02,.36,.74,.16,.07,.3,12],[-1.9,1.14,.30,1.0,.14,.06,.3,15],[-3.1,1.42,0,1.46,0,0,0,15]],
  "mats": [wing, wing, edge, "lavender"]}]}
V["C_stilettos"] = {"omit": OMIT, "lofts": [{
  "sections": [[1.40,.66,.05,.31,.03,.03,.5,0],[0.2,.88,.15,.45,.05,.04,.5,5],[-1.6,.98,.22,.72,.07,.05,.5,8],[-2.9,1.20,.19,.95,.06,.05,.4,10],[-4.75,1.60,0,1.36,0,0,0,10]],
  "mats": [wing, wing, wing, "lavender"]}]}
V["D_canted_blades"] = {"omit": OMIT, "lofts": [{
  "sections": [[1.3,.68,.05,.32,.03,.03,.4,0],[0.0,.88,.18,.48,.05,.04,.4,15],[-1.2,1.00,.32,.76,.06,.05,.4,40],[-2.2,1.12,.30,1.02,.06,.05,.4,55],[-3.6,1.35,0,1.55,0,0,0,55]],
  "mats": [wing, wing, wing, "lavender"]}]}
json.dump(V, open(r"C:/Users/amind/AppData/Local/Temp/claude/882050741e06a72d/835e026b-a642-43ff-8ffe-027d59d4c3dd/scratchpad/minions/fable/variants_side.json", "w"), indent=1)
