## Why add fast traversal?

Approved in the independent PR-1 session: measure safe travel toward a distant fixed point alongside pursuit, without replacing historical acceptance. Stop the unrelated physics-isolation investigation. Reuse MpcSolverRig and the production solver/plant; no new physics harness or runtime tuning in the first measurement.

## What is frozen before measurement?

30 seconds from rest, 50Hz, no excluded warmup. Goal at (0, 2*maxSpeed*30), outside maximum travel reach. POS and FIELD weight1 plus radial velocity request +maxSpeed toward referent1, weight1. Same asset and turn-away/collision costs in both arms; only terminal weight differs (asset3 versus0). Dense stationary circles: 16m square lattice with independent +/-4m jitter per axis, radii2.5–4m, covering the reachable disk plus an obstacle margin; 12m spawn clearing. Development terrain/solver seeds4101–4105; held-out4201–4220. These seeds are new and distinct from prior acceptance sets.

Primary metric is (initial goal range - final goal range)/30s. Acceptance: zero swept collision steps on every field-on seed and field-on median progress speed >=1.10*field-off median. Report both arms' collisions without excluding failed episodes. Diagnostics: path length/30 actual speed, seconds with instantaneous goalward speed below5% maxSpeed, net progress/path length route efficiency, and progress speed divided by paired empty-course speed. Empty controls run both weights on every seed. Swept collision uses the existing rig bank-adjusted hull radius plus obstacle radius, including tangency and the final step. This remains solver-model evidence, not Unity collision proof.

## How will this guide implementation?

Run five development seeds first; inspect trajectories and terminal costs in any failing or slower episode before changing the implementation. Freeze any revised settings before held-out measurement. No broad repeat sweeps. Preserve every attempt. Only promising behavior proceeds to live Unity verification. No acceptance threshold or pursuit requirement is removed.
