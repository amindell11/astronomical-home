# Restore the initial Valis hull prototype

Source commit `36931e9f352a94efa59c9991a99e6b7418389387` restores the prototype at `c51bfd8f05b726911e9e019f1329713a3d322320`, as the owner requested on 2026-10-20. The rollback reverses #1029 and reapplies that initial commit. All thirteen paths touched by #1029 match the original Git objects, including absence of the later brush assets and exporter test.

The shader and two Valis armor materials have the original settings. The original residual diagonal and limitations of separating brush detail from coarse masks return. Other changes on main remain in place.

The new graphics batch passed all four original checks. They cover imported masks, flash restoration in both modes, light response, fixed ink and camera independence. Eighteen paint-tool tests passed. ReSharper reported zero blocking findings. The adjacent reports contain the results.

The [original matched capture](https://github.com/amindell11/astronomical-home/blob/74012bb9602941edb9072fb53d174044153888ec/paint/hull-lighting/README.md) shows this restored shader. It is historical evidence, not a newly recorded capture. The current restoration is verified by Git object equality and the new graphics tests.
