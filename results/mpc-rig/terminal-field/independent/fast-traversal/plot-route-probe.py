from pathlib import Path
import csv
import matplotlib.pyplot as plt
from matplotlib.patches import Circle

ROOT = Path(__file__).parent

def rows(path):
    with path.open(encoding="utf-8-sig") as stream:
        return list(csv.DictReader(stream))

runs = {name: max(ROOT.glob(f"route-{name}-*")) for name in ("excess", "raw")}
colors = {"excess": "#2878b5", "raw": "#dd7630"}
fig, axes = plt.subplots(2, 2, figsize=(11, 8))
for name, run in runs.items():
    data = rows(run / "center.csv")
    axes[0, 0].plot([float(r["posX"]) for r in data], [float(r["posY"]) for r in data], label=name, color=colors[name])
    for case, style in (("center", "-"), ("empty", "--")):
        data = [r for r in rows(run / f"{case}.csv") if float(r["t"]) <= 1]
        axes[0, 1].plot([float(r["t"]) for r in data], [float(r["posX"]) for r in data], style, label=f"{name}, {case}", color=colors[name])
    summary = {r["case"]: float(r["minimumClearance"]) for r in rows(run / "summary.csv")}
    cases = ("center", "left", "right", "center-wide")
    offset = -.18 if name == "excess" else .18
    axes[1, 1].bar([i + offset for i in range(4)], [summary[c] for c in cases], .36, label=name, color=colors[name])

axes[0, 0].add_patch(Circle((0, 40), 4, color="#6a6a6a", alpha=.5))
axes[0, 0].add_patch(Circle((0, 40), 5, fill=False, linestyle=":"))
axes[0, 0].set(xlim=(-10, 10), ylim=(25, 55), xlabel="Lateral position (m)", ylabel="Forward position (m)", title="Both variants hit the centered rock")
axes[0, 0].set_aspect("equal", adjustable="box")
axes[0, 1].set(xlabel="Time (s)", ylabel="Lateral position (m)", title="Early steering also appears without the rock")
cross = [r for r in rows(ROOT / "route-value-comparison.csv") if r["kind"] == "cross-section"]
middle = next(r for r in cross if float(r["x"]) == 0)
for key, label, color in (("terminalCost", "excess", colors["excess"]), ("rawCost", "raw route", colors["raw"])):
    axes[1, 0].plot([float(r["x"]) for r in cross], [float(r[key]) - float(middle[key]) for r in cross], label=label, color=color)
axes[1, 0].axvspan(-1.62, 1.62, color="gray", alpha=.15, label="sampled endpoint x range")
axes[1, 0].set(xlabel="Endpoint x at y = 17 m", ylabel="Cost relative to x = 0", title="Raw gradient survives; avoidance still fails")
axes[1, 1].axhline(0, color="black", linewidth=.8)
axes[1, 1].set(xticks=range(4), xticklabels=cases, ylabel="Minimum swept clearance (m)", title="Negative clearance means collision")
for ax in axes.flat:
    ax.grid(alpha=.2)
    ax.legend(fontsize=8)
fig.suptitle("Rejected full-route-distance prototype | 0.7 s horizon, 4 s encounters", fontsize=14)
fig.tight_layout()
fig.savefig(ROOT / "route-value-probe.png", dpi=170)
