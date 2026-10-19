# Valis hull lighting prototype

Source commit: `c51bfd8f05b726911e9e019f1329713a3d322320`. Relates to issue #1027. Owner approved building a prototype, not merging or fleet rollout.

The comparison uses the same Valis prefab, pose, camera and light on both sides. Left disables `_LIGHT_DRIVEN_PAINT`; right enables it only on jade-iris Ivory and Cool gray armor. No geometry, UV, masks or canopies change. Side-wall treatment remains authored. The camera renders the ship at inspection size and about 100 pixels tall.

The ten-second clip has three equal sections: ship bank/turn under a fixed directional light; a moving directional light with a stationary ship; camera orbit with fixed ship and light. These are controlled Unity Editor renders under the Performant quality level, not gameplay, a performance measurement, or proof of other palettes/ships. The render tests use HighFidelity.

The shader uses a coarse mip of the existing grayscale Shadow/Light masks to bias the light threshold and keeps their finer color variation at the original UVs. Ink remains separate. This is approximate separation: the existing masks mix brush texture, structural darkness and directional shading. Flat faces do not contain authored normal directions, so equal-facing light directions cannot independently reposition their shading. Extra authored lighting data remains an option after reviewing this prototype.

An initial pass applied this to every non-engine hull material; it brightened structural trim too much. The final prototype limits participation to the two broad armor materials. Sliding mask UVs was rejected because it would move surface marks. Colored mask extensions are outside this experiment; Crimson is preparing its grayscale migration separately.

`HullCapture.cs` is the capture source; adjust its output root for another slot and run its Motion entry in a coordinator-leased Unity Editor. `publish.py` labels the frames and encodes the comparison. `render-tests.json` records four passing checks: mask import, damage-flash restoration in both shader modes, and light/camera/ink independence. `resharper-summary.json` records the required quality ratchet. The combined code review requested no changes.
