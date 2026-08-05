"""Bench replicate driver: one invocation runs the canonical roster bench through the eval
lane — R sequential replicates plus the mirror — then aggregates the read that was previously
hand-written (results/rl-eval/rebaseline-699941-2026-07-31/AGGREGATE.md is the model).

Every session goes through eval_lane.run_eval_lane, so launch, coordination, env composition,
and summary read-back stay the lane's. Replicates run strictly sequentially: base-port 5006
is single-occupancy machine-wide.

    cd training/rl
    .venv\\Scripts\\python bench_replicates.py --onnx <ckpt.onnx> --project D:/amind/git/agent-N/src/Asteroids3D

Outputs land in the run root: rep-01..rep-NN/ and mirror/ session dirs, plus AGGREGATE.md and
AGGREGATE.json. Probe sidecars are collected and tabulated from the summaries the sessions
emitted — never re-derived; probe names are an opaque pass-through string.
"""
import argparse
import hashlib
import json
import subprocess
import sys
from datetime import datetime
from pathlib import Path
from statistics import mean, stdev

from driver_common import default_unity_exe
import eval_lane

RL_DIR = Path(__file__).resolve().parent
REPO_ROOT = RL_DIR.parent.parent
PROJECT = REPO_ROOT / "src" / "Asteroids3D"
# Staging discipline (eval_lane.MANUAL_ROOT precedent): bench artifacts land in the primary tree.
BENCH_ROOT = Path(r"D:\amind\git\astronomical-home") / "results" / "rl-eval"

ROSTER_SEEDS = "2001,2002,2003,2004,2005"
AGGREGATE_SCHEMA = "rl-bench-aggregate-v1"


def session_cells(summary: dict, episodes: list) -> dict:
    """Per-opponent outcome cell for one session: summary counts plus timeout counts, which
    only the episode JSONL carries. The two files are one producer's outputs, so a win-count
    disagreement between them is a broken contract, not data to absorb."""
    cells = {row["opponent"]: {"episodes": row["episodes"], "wins": row["wins"],
                               "losses": row["losses"], "draws": row["draws"], "timeouts": 0}
             for row in summary["opponents"]}
    jsonl_wins = {opp: 0 for opp in cells}
    for ep in episodes:
        opp = ep["opponent"]["archetype"]
        if opp not in cells:
            raise ValueError(f"episode row names opponent {opp!r} absent from the summary")
        if ep["timedOut"]:
            cells[opp]["timeouts"] += 1
        if ep["outcome"] == "Win":
            jsonl_wins[opp] += 1
    for opp, cell in cells.items():
        if cell["wins"] != jsonl_wins[opp]:
            raise ValueError(f"summary says {cell['wins']} wins vs {opp} but the episode "
                             f"JSONL has {jsonl_wins[opp]}")
    return cells


def tabulate_probes(sidecars_per_session: list) -> dict:
    """Mean of every numeric sidecar field, per probe per opponent, across sessions.
    Sidecars are {probe: {"opponents": [row, ...]}}; probe identity stays opaque."""
    names = {frozenset(s) for s in sidecars_per_session}
    if len(names) > 1:
        raise ValueError(f"sessions disagree on which probes ran: {sorted(map(sorted, names))}")
    tabulated = {}
    for probe in sorted(names.pop() if names else []):
        by_opp = {}
        for sidecar in sidecars_per_session:
            for row in sidecar[probe]["opponents"]:
                # Sidecar grammars differ per probe: facing labels rows "opponent", gate/combat "archetype".
                label = row.get("opponent") or row.get("archetype")
                if label is None:
                    raise ValueError(f"probe {probe!r} row carries neither 'opponent' nor 'archetype'")
                for field, value in row.items():
                    if isinstance(value, (int, float)) and not isinstance(value, bool):
                        by_opp.setdefault(label, {}).setdefault(field, []).append(value)
        tabulated[probe] = {opp: {field: mean(vals) for field, vals in fields.items()}
                            for opp, fields in by_opp.items()}
    return tabulated


def build_aggregate(*, run_name: str, checkpoint: dict, protocol: dict, rep_reads: list,
                    mirror_read=None) -> dict:
    """The one aggregate structure both outputs render from. Each read is
    {"dir", "summary", "cells", "probes"}; mirror_read is None when --no-mirror."""
    opponents = sorted(rep_reads[0]["cells"])
    for read in rep_reads:
        if sorted(read["cells"]) != opponents:
            raise ValueError(f"{read['dir']} ran opponents {sorted(read['cells'])}, "
                             f"expected {opponents}")
    totals = [sum(c["wins"] for c in read["cells"].values()) for read in rep_reads]
    episodes_per_rep = sum(c["episodes"] for c in rep_reads[0]["cells"].values())
    n = len(rep_reads)
    total_stdev = stdev(totals) if n > 1 else None
    per_opponent = {}
    for opp in opponents:
        cells = [read["cells"][opp] for read in rep_reads]
        wins = [cell["wins"] for cell in cells]
        draws = [cell["draws"] for cell in cells]
        timeouts = [cell["timeouts"] for cell in cells]
        per_opponent[opp] = {
            "episodes": cells[0]["episodes"],
            "meanWins": mean(wins),
            "stdevWins": stdev(wins) if n > 1 else None,
            "winsPerRep": wins,
            "draws": sum(draws),
            "drawsPerRep": draws,
            "timeouts": sum(timeouts),
            "timeoutsPerRep": timeouts,
        }
    return {
        "schema": AGGREGATE_SCHEMA,
        "generatedAt": datetime.now().isoformat(timespec="seconds"),
        "runName": run_name,
        "checkpoint": checkpoint,
        "protocol": protocol,
        "replicates": [{"dir": read["dir"], "summary": read["summary"],
                        "total": total, "opponents": read["cells"]}
                       for read, total in zip(rep_reads, totals)],
        "totals": {"episodesPerRep": episodes_per_rep, "perRep": totals,
                   "mean": mean(totals), "stdev": total_stdev,
                   "min": min(totals), "max": max(totals),
                   "spread": max(totals) - min(totals),
                   "sem": total_stdev / n ** 0.5 if total_stdev is not None else None},
        "perOpponent": per_opponent,
        "mirror": None if mirror_read is None else
                  {"dir": mirror_read["dir"], "summary": mirror_read["summary"],
                   "opponents": mirror_read["cells"]},
        "probes": {"roster": tabulate_probes([read["probes"] for read in rep_reads]),
                   "mirror": None if mirror_read is None else
                             tabulate_probes([mirror_read["probes"]])},
    }


def _fmt(v) -> str:
    if v is None:
        return "—"
    if isinstance(v, float):
        return f"{v:.4g}"
    return str(v)


def _probe_table(tabulated: dict) -> list:
    lines = []
    for probe, by_opp in tabulated.items():
        opponents = sorted(by_opp)
        fields = sorted({f for rows in by_opp.values() for f in rows})
        lines += [f"### `{probe}` probe", "",
                  "| metric | " + " | ".join(opponents) + " |",
                  "|" + "---|" * (len(opponents) + 1)]
        lines += [f"| {field} | " + " | ".join(_fmt(by_opp[opp].get(field)) for opp in opponents) + " |"
                  for field in fields]
        lines.append("")
    return lines


def render_markdown(agg: dict) -> str:
    proto, totals = agg["protocol"], agg["totals"]
    opponents = sorted(agg["perOpponent"])
    n = len(agg["replicates"])
    lines = [
        f"# {agg['runName']} — bench aggregate",
        "",
        f"**Checkpoint:** `{agg['checkpoint']['path']}`, md5 `{agg['checkpoint']['md5']}`",
        f"**Project:** `{agg['checkpoint']['project']}` @ `{agg['checkpoint']['projectHead']}`",
        f"**Protocol:** seeds {proto['seeds']}, {proto['episodesPerSeed']} episodes/seed, "
        f"density {proto['density']}, roster × {n} replicate(s)"
        + (", + mirror" if agg["mirror"] else ", no mirror")
        + f", probes: {proto['probes']}.",
        f"Lane: `eval_lane.py` driven by `bench_replicates.py` ({agg['generatedAt']}).",
        "",
        "## Roster result",
        "",
        "| rep | total | " + " | ".join(opponents) + " |",
        "|" + "---|" * (len(opponents) + 2),
    ]
    for rep in agg["replicates"]:
        lines.append(f"| {rep['dir']} | {rep['total']}/{totals['episodesPerRep']} | "
                     + " | ".join(str(rep["opponents"][opp]["wins"]) for opp in opponents) + " |")
    lines += [
        "",
        f"**MEAN {totals['mean']:.2f} / {totals['episodesPerRep']}** (n={n}) — "
        f"min {totals['min']}, max {totals['max']}, spread {totals['spread']}, "
        f"stdev {_fmt(totals['stdev'])}, SEM {_fmt(totals['sem'])}.",
        "",
        "Per-archetype mean wins: " + " · ".join(
            f"{opp} {agg['perOpponent'][opp]['meanWins']:.2f}/{agg['perOpponent'][opp]['episodes']}"
            for opp in opponents),
        "",
        "## Draws & timeouts (summed across replicates)",
        "",
        "| archetype | draws | timeouts | draws/rep | timeouts/rep |",
        "|---|---|---|---|---|",
    ]
    for opp in opponents:
        cell = agg["perOpponent"][opp]
        lines.append(f"| {opp} | {cell['draws']} | {cell['timeouts']} | "
                     f"{','.join(map(str, cell['drawsPerRep']))} | "
                     f"{','.join(map(str, cell['timeoutsPerRep']))} |")
    lines.append("")
    if agg["mirror"]:
        cells = agg["mirror"]["opponents"]
        episodes = sum(c["episodes"] for c in cells.values())
        wins = sum(c["wins"] for c in cells.values())
        losses = sum(c["losses"] for c in cells.values())
        draws = sum(c["draws"] for c in cells.values())
        timeouts = sum(c["timeouts"] for c in cells.values())
        lines += [f"## Mirror ({episodes} episodes)", "",
                  f"wins {wins} · losses {losses} · draws {draws} · timeouts {timeouts}", ""]
    if agg["probes"]["roster"]:
        lines += ["## Probes — roster (mean across replicates)", ""]
        lines += _probe_table(agg["probes"]["roster"])
    if agg["mirror"] and agg["probes"]["mirror"]:
        lines += ["## Probes — mirror", ""]
        lines += _probe_table(agg["probes"]["mirror"])
    return "\n".join(lines).rstrip() + "\n"


def load_read(dir_name: str, summary_path: Path) -> dict:
    """One session's aggregate input, following the paths the summary itself emitted."""
    summary = json.loads(summary_path.read_text(encoding="utf-8"))
    with open(summary["episodesJsonl"], encoding="utf-8") as f:
        episodes = [json.loads(line) for line in f if line.strip()]
    probes = {p["name"]: json.loads(Path(p["summary"]).read_text(encoding="utf-8"))
              for p in summary.get("probes", [])}
    return {"dir": dir_name, "summary": str(summary_path),
            "cells": session_cells(summary, episodes), "probes": probes}


def run_bench(*, onnx: Path, replicates: int, seeds: str, episodes_per_seed, density,
              probes, mirror: bool, run_root: Path, project: Path, unity: Path,
              lease: str, lease_wait: int) -> tuple:
    """The full protocol, strictly sequential; returns (rep summary paths, mirror summary or None)."""
    rep_summaries = []
    for r in range(1, replicates + 1):
        print(f"[bench] roster replicate {r}/{replicates}")
        rep_summaries.append(eval_lane.run_eval_lane(
            project=project, unity=unity, lease=lease, out_dir=run_root / f"rep-{r:02d}",
            onnx=onnx, seeds=seeds, episodes_per_seed=episodes_per_seed, density=density,
            probes=probes, lease_wait=lease_wait))
    mirror_summary = None
    if mirror:
        print("[bench] mirror")
        mirror_summary = eval_lane.run_eval_lane(
            project=project, unity=unity, lease=lease, out_dir=run_root / "mirror",
            onnx=onnx, seeds=seeds, episodes_per_seed=episodes_per_seed, density=density,
            opponent="mirror", probes=probes, lease_wait=lease_wait)
    return rep_summaries, mirror_summary


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--onnx", type=Path, required=True, help="checkpoint .onnx to bench")
    parser.add_argument("--replicates", type=int, default=2, help="roster replicate count R")
    parser.add_argument("--seeds", default=ROSTER_SEEDS, help="RL_HARNESS_SEEDS selection")
    parser.add_argument("--episodes-per-seed", default=3, help="RL_HARNESS_EPISODES_PER_SEED")
    parser.add_argument("--density", default=2.0, help="RL_HARNESS_DENSITY")
    parser.add_argument("--probes", default="facing",
                        help="RL_HARNESS_PROBES pass-through (opaque; e.g. facing,controller)")
    parser.add_argument("--no-mirror", action="store_true", help="skip the mirror session")
    parser.add_argument("--run-name", default=None,
                        help="run-root dir name (default: bench-<ckpt-stem>-<timestamp>)")
    parser.add_argument("--out-root", type=Path, default=BENCH_ROOT,
                        help="where the run root is created")
    parser.add_argument("--project", type=Path, default=PROJECT,
                        help="Unity project the sessions boot in (point at a free pool slot)")
    parser.add_argument("--unity", type=Path, default=None,
                        help="Unity.exe path (default: derived from ProjectVersion.txt)")
    parser.add_argument("--lease", default="rl-bench", help="unity-access lease name")
    parser.add_argument("--lease-wait", type=int, default=1800,
                        help="seconds the coordinator may wait for the project/boot lane")
    args = parser.parse_args()
    if not args.onnx.exists():
        parser.error(f"--onnx {args.onnx} does not exist")
    if args.replicates < 1:
        parser.error(f"--replicates must be >= 1, got {args.replicates}")
    if not args.project.exists():
        parser.error(f"--project {args.project} does not exist")

    run_name = args.run_name or f"bench-{args.onnx.stem}-{datetime.now().strftime('%Y%m%d-%H%M%S')}"
    run_root = args.out_root / run_name
    if run_root.exists():
        sys.exit(f"FAIL: run root {run_root} already exists; pick a fresh --run-name")
    run_root.mkdir(parents=True)
    unity = args.unity or default_unity_exe(args.project)
    print(f"[bench] {run_name}  project {args.project}  R={args.replicates}  artifacts {run_root}")

    rep_summaries, mirror_summary = run_bench(
        onnx=args.onnx, replicates=args.replicates, seeds=args.seeds,
        episodes_per_seed=args.episodes_per_seed, density=args.density, probes=args.probes,
        mirror=not args.no_mirror, run_root=run_root, project=args.project, unity=unity,
        lease=args.lease, lease_wait=args.lease_wait)

    checkpoint = {
        "path": str(args.onnx),
        "md5": hashlib.md5(args.onnx.read_bytes()).hexdigest(),
        "project": str(args.project),
        "projectHead": subprocess.run(
            ["git", "-C", str(args.project), "rev-parse", "--short", "HEAD"],
            capture_output=True, text=True, check=True).stdout.strip(),
    }
    protocol = {"seeds": args.seeds, "episodesPerSeed": args.episodes_per_seed,
                "density": args.density, "replicates": args.replicates,
                "probes": args.probes, "mirror": not args.no_mirror}
    agg = build_aggregate(
        run_name=run_name, checkpoint=checkpoint, protocol=protocol,
        rep_reads=[load_read(p.parent.name, p) for p in rep_summaries],
        mirror_read=None if mirror_summary is None else load_read("mirror", mirror_summary))
    (run_root / "AGGREGATE.json").write_text(json.dumps(agg, indent=1), encoding="utf-8")
    (run_root / "AGGREGATE.md").write_text(render_markdown(agg), encoding="utf-8")
    print(f"[bench] aggregate {run_root / 'AGGREGATE.md'}")


if __name__ == "__main__":
    main()
