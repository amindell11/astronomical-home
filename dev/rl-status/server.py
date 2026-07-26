"""At-a-glance status page for a live run_parallel.py training run.

Regenerates from the trainer log + episode JSONLs on every request, so the page is always
current; the browser re-requests on a meta refresh. Read-only: it never touches run state.

    python dev/rl-status/server.py [--run-id ship_combat_stage2] [--port 8765]
"""
import argparse
import collections
import glob
import html
import json
import os
import re
import subprocess
import time
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SUMMARY_RE = re.compile(r"Step:\s*(\d+)\.\s*Time Elapsed:\s*([\d.]+)\s*s.*?Mean Reward:\s*(-?\d+(?:\.\d+)?)"
                        r"(?:.*?ELO:\s*(-?\d+(?:\.\d+)?))?")
LESSON_RE = re.compile(r"Parameter '(\w+)' is in lesson '([\w.]+)' and has value '([^']+)'")
FAIL_RE = re.compile(r"Traceback|UnityWorker|Learning was interrupted|timed out|Killed|OOM|CUDA error")
TAIL_BYTES = 4_000_000
RECENT_EPISODES = 240


def tail_lines(path, nbytes=TAIL_BYTES):
    try:
        size = os.path.getsize(path)
        with open(path, "rb") as fh:
            if size > nbytes:
                fh.seek(size - nbytes)
                fh.readline()  # drop the partial line
            return fh.read().decode("utf-8", "replace").splitlines()
    except OSError:
        return []


def trainer_state(run_id):
    log = REPO / "results" / "rl-training" / f"{run_id}-parallel-trainer.log"
    out = {"log": log, "exists": log.exists(), "summaries": [], "lessons": {}, "failures": [],
           "log_age": None, "max_steps": None}
    if not log.exists():
        return out
    out["log_age"] = time.time() - log.stat().st_mtime
    lines = tail_lines(log)
    for ln in lines:
        m = SUMMARY_RE.search(ln)
        if m:
            out["summaries"].append((int(m.group(1)), float(m.group(2)), float(m.group(3)),
                                     float(m.group(4)) if m.group(4) else None))
            continue
        m = LESSON_RE.search(ln)
        if m:
            out["lessons"][m.group(1)] = (m.group(2), m.group(3))
            continue
        if FAIL_RE.search(ln):
            out["failures"].append(ln.strip()[:220])
    for ln in lines:
        if "max_steps:" in ln:
            try:
                out["max_steps"] = int(ln.split("max_steps:")[1].strip())
            except (ValueError, IndexError):
                pass
    return out


def episode_rows(run_id):
    """Episodes from the newest JSONL group that belongs to THIS run.

    The launcher names groups by wall-clock, not run id, so 'newest group' alone attributes a
    live run's episodes to whatever run the page is showing. Bound the group to the window the
    run was actually writing in: after its dir appeared, before its trainer log went quiet.
    """
    files = glob.glob(str(REPO / "results" / "rl-episodes" / "*-training-w*.jsonl"))
    if not files:
        return [], None
    run_dir = REPO / "results" / "rl-training" / run_id
    log = REPO / "results" / "rl-training" / f"{run_id}-parallel-trainer.log"
    try:
        start = run_dir.stat().st_ctime
        end = log.stat().st_mtime + 300  # margin: the final flush trails the last log write
    except OSError:
        start, end = 0, float("inf")

    stamps = collections.defaultdict(list)
    for f in files:
        stamps[Path(f).name.split("-training-")[0]].append(f)

    def group_time(name):
        try:
            return time.mktime(time.strptime(name, "%Y%m%d-%H%M%S"))
        except ValueError:
            return None

    owned = [g for g in stamps
             if (gt := group_time(g)) is not None and start - 300 <= gt <= end]
    if not owned:
        return [], None
    newest = max(owned)
    rows = []
    for f in stamps[newest]:
        for ln in tail_lines(f):
            ln = ln.strip()
            if not ln:
                continue
            try:
                r = json.loads(ln)
            except json.JSONDecodeError:
                continue
            r.pop("trace", None)
            rows.append(r)
    rows.sort(key=lambda r: r.get("episodeIndex", 0))
    return rows, newest


def archetype_table(rows):
    by = collections.defaultdict(list)
    for r in rows:
        by[r.get("opponent", {}).get("archetype", "?")].append(r)
    table = []
    for arch, sub in sorted(by.items()):
        dmg = [r.get("startEnemyPool", 0) - r.get("endEnemyPool", 0) for r in sub]
        outcomes = collections.Counter(r.get("outcome") for r in sub)
        table.append({
            "arch": arch, "n": len(sub),
            "landed": sum(1 for d in dmg if d > 0) / len(sub),
            "mean_dmg": sum(dmg) / len(sub),
            "win": outcomes.get("Win", 0), "draw": outcomes.get("Draw", 0),
            "loss": outcomes.get("Loss", 0),
            "timeout": sum(1 for r in sub if r.get("timedOut")) / len(sub),
            "oob": sum(1 for r in sub if r.get("agentExitedBounds")) / len(sub),
        })
    return sorted(table, key=lambda t: -t["n"])


def density_table(rows):
    """Self-play view: the opponent is the mirror, so archetype is blank and win rate is ~50% by
    construction. What varies is the sampled density band — draws and episode length across it are
    the stalemate signal a symmetric win rate cannot show."""
    buckets = [("0.5-1.0", 0.5, 1.0), ("1.0-1.5", 1.0, 1.5), ("1.5-2.0", 1.5, 2.0), ("2.0-2.5", 2.0, 2.51)]
    table = []
    for label, lo, hi in buckets:
        sub = [r for r in rows if lo <= r.get("spec", {}).get("fieldDensityScale", -1) < hi]
        if not sub:
            continue
        n = len(sub)
        outcomes = collections.Counter(r.get("outcome") for r in sub)
        table.append({
            "band": label, "n": n,
            "win": outcomes.get("Win", 0) / n,
            "draw": outcomes.get("Draw", 0) / n,
            "secs": sum(r.get("simSeconds", 0) for r in sub) / n,
            "dmg": sum(r.get("startEnemyPool", 0) - r.get("endEnemyPool", 0) for r in sub) / n,
            "mutual": sum(1 for r in sub if r.get("mutualKill")) / n,
        })
    return table


def workers_alive():
    try:
        out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq RLTraining.exe", "/NH"],
                             capture_output=True, text=True, timeout=10).stdout
        return out.count("RLTraining.exe")
    except Exception:
        return -1


def free_ram_gb():
    """ctypes rather than wmic: wmic is absent on current Windows builds."""
    try:
        import ctypes

        class MemStatus(ctypes.Structure):
            _fields_ = [("dwLength", ctypes.c_ulong), ("dwMemoryLoad", ctypes.c_ulong),
                        ("ullTotalPhys", ctypes.c_ulonglong), ("ullAvailPhys", ctypes.c_ulonglong),
                        ("ullTotalPageFile", ctypes.c_ulonglong), ("ullAvailPageFile", ctypes.c_ulonglong),
                        ("ullTotalVirtual", ctypes.c_ulonglong), ("ullAvailVirtual", ctypes.c_ulonglong),
                        ("ullAvailExtendedVirtual", ctypes.c_ulonglong)]

        st = MemStatus()
        st.dwLength = ctypes.sizeof(MemStatus)
        ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(st))
        return st.ullAvailPhys / 1024 ** 3
    except Exception:
        return None


def sparkline(values, w=460, h=54):
    if len(values) < 2:
        return ""
    lo, hi = min(values), max(values)
    span = (hi - lo) or 1.0
    step = w / (len(values) - 1)
    pts = " ".join(f"{i * step:.1f},{h - (v - lo) / span * (h - 6) - 3:.1f}"
                   for i, v in enumerate(values))
    return (f'<svg viewBox="0 0 {w} {h}" preserveAspectRatio="none" class="spark">'
            f'<polyline points="{pts}" fill="none" stroke="currentColor" stroke-width="2"/>'
            f'</svg><div class="sparkax"><span>{lo:.2f}</span><span>{hi:.2f}</span></div>')


def human_dt(seconds):
    if seconds is None:
        return "-"
    seconds = int(seconds)
    h, rem = divmod(seconds, 3600)
    m, s = divmod(rem, 60)
    return f"{h}h {m:02d}m" if h else f"{m}m {s:02d}s"


def render(run_id):
    t = trainer_state(run_id)
    rows, stamp = episode_rows(run_id)
    recent = rows[-RECENT_EPISODES:]
    workers = workers_alive()
    ram = free_ram_gb()

    summaries = t["summaries"]
    step = summaries[-1][0] if summaries else 0
    elapsed = summaries[-1][1] if summaries else 0
    reward = summaries[-1][2] if summaries else 0.0
    elos = [s[3] for s in summaries if s[3] is not None]
    # The league writes ELO on every summary; the scripted roster never does.
    selfplay = bool(elos)
    elo = elos[-1] if elos else None
    max_steps = t["max_steps"] or 3_500_000
    pct = min(100.0, step / max_steps * 100) if max_steps else 0

    window = summaries[-10:]
    rate = None
    if len(window) >= 2 and window[-1][1] > window[0][1]:
        rate = (window[-1][0] - window[0][0]) / (window[-1][1] - window[0][1])
    overall = step / elapsed if elapsed else None
    eta = (max_steps - step) / rate if rate else None

    rate_s = f"{rate:.0f}" if rate else "-"
    overall_s = f"{overall:.0f}/s" if overall else "-"
    ram_s = f"{ram:.1f} GB" if ram is not None else "n/a"
    stalled = t["log_age"] is not None and t["log_age"] > 300
    if workers == 0 and step < max_steps * 0.99:
        state, cls = "STOPPED", "bad"
    elif stalled:
        state, cls = "STALLED?", "warn"
    elif step >= max_steps * 0.99:
        state, cls = "COMPLETE", "good"
    else:
        state, cls = "RUNNING", "good"

    p = [f"""<!doctype html><html><head><meta charset="utf-8">
<meta http-equiv="refresh" content="20"><title>{html.escape(run_id)} - training status</title>
<style>
:root{{color-scheme:light dark;--bg:#0f1115;--fg:#e6e8ee;--dim:#9aa3b2;--card:#171a21;--line:#252a34;
--good:#3fb950;--warn:#d29922;--bad:#f85149;--accent:#58a6ff}}
@media(prefers-color-scheme:light){{:root{{--bg:#f6f7f9;--fg:#1c2128;--dim:#5b6472;--card:#fff;--line:#e3e6ea}}}}
*{{box-sizing:border-box}}
body{{margin:0;padding:22px;background:var(--bg);color:var(--fg);
font:14px/1.45 ui-monospace,SFMono-Regular,Menlo,Consolas,monospace}}
h1{{font-size:17px;margin:0 0 2px;letter-spacing:.3px}}
.sub{{color:var(--dim);font-size:12px;margin-bottom:16px}}
.grid{{display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:12px;margin-bottom:14px}}
.card{{background:var(--card);border:1px solid var(--line);border-radius:9px;padding:12px 14px}}
.k{{color:var(--dim);font-size:11px;text-transform:uppercase;letter-spacing:.7px}}
.v{{font-size:22px;font-weight:600;margin-top:3px}}
.v small{{font-size:12px;font-weight:400;color:var(--dim)}}
.good{{color:var(--good)}}.warn{{color:var(--warn)}}.bad{{color:var(--bad)}}
.bar{{height:9px;background:var(--line);border-radius:5px;overflow:hidden;margin-top:9px}}
.bar>div{{height:100%;background:var(--accent)}}
table{{width:100%;border-collapse:collapse;font-size:13px}}
th,td{{text-align:right;padding:6px 9px;border-bottom:1px solid var(--line)}}
th:first-child,td:first-child{{text-align:left}}
th{{color:var(--dim);font-weight:500;font-size:11px;text-transform:uppercase;letter-spacing:.6px}}
.spark{{width:100%;height:54px;color:var(--accent);display:block}}
.sparkax{{display:flex;justify-content:space-between;color:var(--dim);font-size:11px}}
.wide{{background:var(--card);border:1px solid var(--line);border-radius:9px;padding:12px 14px;margin-bottom:14px}}
.lessons{{display:flex;flex-wrap:wrap;gap:7px;margin-top:8px}}
.chip{{border:1px solid var(--line);border-radius:14px;padding:3px 10px;font-size:12px;color:var(--dim)}}
.chip b{{color:var(--fg);font-weight:600}}
.fail{{color:var(--bad);font-size:12px;white-space:pre-wrap;margin-top:6px}}
.foot{{color:var(--dim);font-size:11px;margin-top:16px}}
</style></head><body>
<h1>{html.escape(run_id)} <span class="{cls}">&#9679; {state}</span></h1>
<div class="sub">refreshes every 20s &middot; generated {time.strftime('%H:%M:%S')} &middot;
episodes file group {html.escape(stamp or '-')}</div>
<div class="grid">
  <div class="card"><div class="k">Steps</div><div class="v">{step:,} <small>/ {max_steps:,}</small></div>
    <div class="bar"><div style="width:{pct:.2f}%"></div></div>
    <div class="k" style="margin-top:6px">{pct:.1f}% complete</div></div>
  <div class="card"><div class="k">Throughput</div>
    <div class="v">{rate_s} <small>steps/s</small></div>
    <div class="k" style="margin-top:6px">overall {overall_s}</div></div>
  <div class="card"><div class="k">ETA remaining</div><div class="v">{human_dt(eta)}</div>
    <div class="k" style="margin-top:6px">elapsed {human_dt(elapsed)}</div></div>
  <div class="card"><div class="k">{'League ELO' if selfplay else 'Mean reward'} <small>{'(not skill)' if selfplay else '(blended)'}</small></div>
    <div class="v">{f'{elo:.0f}' if selfplay else f'{reward:+.3f}'}</div>
    <div class="k" style="margin-top:6px">{'mean reward ~0 by symmetry' if selfplay else 'read the table, not this'}</div></div>
  <div class="card"><div class="k">Workers</div>
    <div class="v {'good' if workers > 0 else 'bad'}">{workers}</div>
    <div class="k" style="margin-top:6px">free RAM {ram_s}</div></div>
  <div class="card"><div class="k">Log age</div>
    <div class="v {'warn' if stalled else ''}">{human_dt(t['log_age'])}</div>
    <div class="k" style="margin-top:6px">since last trainer write</div></div>
</div>""" if summaries else "<body><h1>waiting for the first trainer summary...</h1>"]

    if summaries:
        if selfplay:
            p.append('<div class="wide"><div class="k">League ELO, last %d summaries '
                     '&mdash; a coarse liveness check only: last arc ELO slid 1210&rarr;1140 while the '
                     'drift eval IMPROVED</div>%s</div>'
                     % (len(elos[-60:]), sparkline(elos[-60:])))
        p.append('<div class="wide"><div class="k">Mean reward, last %d summaries%s</div>%s</div>'
                 % (len(summaries[-60:]),
                    ' &mdash; symmetric mirror matches, so this sits near 0 by construction' if selfplay else '',
                    sparkline([s[2] for s in summaries[-60:]])))

        chips = "".join(f'<span class="chip">{html.escape(k)}: <b>{html.escape(v[0])}</b> = '
                        f'{html.escape(v[1])}</span>' for k, v in sorted(t["lessons"].items()))
        p.append(f'<div class="wide"><div class="k">'
                 f'{"Graduation env (constant, no ramp)" if selfplay else "Current curriculum lessons"}</div>'
                 f'<div class="lessons">{chips or "-"}</div></div>')

        if selfplay:
            tbl = density_table(recent)
            head = ("<tr><th>density band</th><th>episodes</th><th>win rate</th><th>draws</th>"
                    "<th>mean secs</th><th>mean dmg</th><th>mutual kills</th></tr>")
            body = "".join(
                f"<tr><td>{html.escape(r['band'])}</td><td>{r['n']}</td>"
                f"<td>{r['win']:.0%}</td><td>{r['draw']:.0%}</td>"
                f"<td>{r['secs']:.1f}</td><td>{r['dmg']:.1f}</td>"
                f"<td>{r['mutual']:.0%}</td></tr>" for r in tbl)
            p.append(f'<div class="wide"><div class="k">By sampled density band, last {len(recent)} episodes '
                     f'(of {len(rows)} this run) &mdash; win rate is ~50% by symmetry; '
                     f'DRAWS and episode length are the stalemate signal</div>'
                     f'<table>{head}{body}</table></div>')
        else:
            tbl = archetype_table(recent)
            head = ("<tr><th>opponent</th><th>episodes</th><th>dmg landed</th><th>mean dmg</th>"
                    "<th>W / D / L</th><th>timeout</th><th>out-of-bounds</th></tr>")
            body = "".join(
                f"<tr><td>{html.escape(r['arch'])}</td><td>{r['n']}</td>"
                f"<td>{r['landed']:.0%}</td><td>{r['mean_dmg']:.1f}</td>"
                f"<td>{r['win']} / {r['draw']} / {r['loss']}</td>"
                f"<td>{r['timeout']:.0%}</td><td>{r['oob']:.0%}</td></tr>" for r in tbl)
            p.append(f'<div class="wide"><div class="k">Per-archetype, last {len(recent)} episodes '
                     f'(of {len(rows)} this run)</div><table>{head}{body}</table></div>')

        if t["failures"]:
            fails = "".join(f'<div class="fail">{html.escape(f)}</div>' for f in t["failures"][-6:])
            p.append(f'<div class="wide"><div class="k bad">Failure signatures in log</div>{fails}</div>')

    p.append('<div class="foot">read-only view of results/rl-training + results/rl-episodes; '
             'blended mean reward hides per-archetype progress by design</div></body></html>')
    return "".join(p)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--run-id", default="ship_combat_stage2")
    ap.add_argument("--port", type=int, default=8765)
    args = ap.parse_args()

    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            try:
                page = render(args.run_id).encode("utf-8")
            except Exception as exc:  # a status page must never take the run's log down with it
                page = f"<pre>status page error: {html.escape(repr(exc))}</pre>".encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(page)))
            self.end_headers()
            self.wfile.write(page)

        def log_message(self, *a):
            pass

    print(f"serving {args.run_id} status on http://127.0.0.1:{args.port}")
    HTTPServer(("127.0.0.1", args.port), Handler).serve_forever()


if __name__ == "__main__":
    main()
