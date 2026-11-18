import json
OMIT = ["20 lower swept blades","21 lower blade lavender tips","21 lower blade lavender tips underside","22 lower blade dark inner facets","40 forward outriggers","41 outrigger lavender toes","42 outrigger inner sockets","51 midship auxiliary vanes","52 auxiliary vane feet","53 auxiliary vane roots","07 rear crest","50 aft split prongs"]
wing = {"top": "gray", "bevel": "ivory", "under": "ivory"}
V = {"rival": {}}
edge = {"top": "gray", "bevel": "lavender", "under": "ivory"}
ridge = {"top": "ivory", "bevel": "gray", "under": "graphite"}
V["A_leaf_sabre"] = {"omit": OMIT, "lofts": [{
  "sections": [[-1.4,.14,1.06,.05,.04,.5],[-2.6,.26,1.12,.09,.07,.5],[-3.5,.58,1.12,.10,.07,.5],[-4.05,.46,1.10,.09,.06,.25],[-5.35,0,1.04,0,0,0]],
  "mats": [wing, wing, edge, "lavender"]}]}
V["B_harpoon"] = {"omit": OMIT, "lofts": [{
  "sections": [[-2.1,.20,1.10,.07,.05,.4],[-3.7,.18,1.06,.07,.06,.4],[-4.0,.62,1.04,.10,.07,.25],[-5.45,0,1.0,0,0,0]],
  "mats": [wing, "lavender", "lavender"]}]}
V["C_scorpion_thorn"] = {"omit": OMIT, "lofts": [{
  "sections": [[-1.2,.34,1.05,.04,.02,.3],[-2.4,.44,1.22,.34,.10,.3],[-3.5,.38,1.62,.36,.12,.3],[-4.2,.26,1.90,.26,.10,.3],[-5.2,0,1.92,0,0,0]],
  "mats": [ridge, ridge, "lavender", "lavender"]}]}
V["D_wasp_sting"] = {"omit": OMIT, "lofts": [{
  "sections": [[-2.7,.40,.34,.10,.26,.3],[-3.45,.46,.34,.12,.28,.3],[-4.1,.32,.34,.12,.20,.3],[-5.45,0,.34,0,0,0]],
  "mats": [wing, wing, "lavender"]}]}
json.dump(V, open("variants.json","w"), indent=1)
