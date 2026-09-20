# What does outside research change about the terminal-field investigation?

Research pass: 2026-09-10. Primary papers and author-hosted material, checked against the independent implementation and its short causal experiments. No material from the other posted implementation was consulted. This is a design assessment, not evidence that a published controller will work unchanged in this game.

## What kind of control problem do we actually have?

The ship has planar position and velocity, yaw and yaw rate, separate forward/reverse thrust, and lateral acceleration that decreases with speed. Yaw has inertia, damping, and bank coupling. It can strafe, so car steering constraints are a poor direct model. At one position, two different velocities or headings can require very different avoidance actions. `Model.Step` provides the available prediction model; live-physics transfer remains a separate validation task.

Our solver samples correlated perturbations around its previous plan, retains improvements over the incumbent, and averages elite controls. It is not the exponential-weighted MPPI algorithm in the papers. The relevant connection is averaging and local sampling in a nonconvex trajectory space.

The field currently supplies an endpoint-only, position-only geometric excess distance. It is a terrain heuristic; we have not established that it approximates the remaining cost of flying the ship. The experiments show spatial undersampling, locally flat values, and a tradeoff between field benefit and running cost. These are separate from sensing range and optimizer history.

## Can good avoidance plans average into a bad one?

Yes, in the published examples. *Path Integral Control with Rollout Clustering and Dynamic Obstacles* demonstrates weighted averages crossing undesirable regions between good trajectory groups and averages within clusters instead. SVG-MPPI instead guides optimization toward one mode of the action distribution. SVG-MPPI also identifies flat gradients as a limitation. [Rollout clustering, introduction and section III-A](https://arxiv.org/html/2403.18066v1), [SVG-MPPI, sections II and VI](https://arxiv.org/html/2309.11040v3).

**Application here:** a left/right split could explain indecision even if the field supplies useful guidance. We have not demonstrated that particular failure in our controller. We did verify that the emitted elite average has a different cost from the reported best sample. In the fine-grid, weight-30 first solve, the emitted cost was 153.8083 versus best sampled cost 151.2794. That alone does not prove a collision or opposing-route cancellation. Test that mechanism before changing selection.

## Does a terminal cost justify a shorter horizon?

MPC terminal ingredients account for behavior after the optimized interval. The full-state value function and a feasible continuation policy are central to the theory; stability results require stated assumptions about costs, constraints, and terminal behavior. An arbitrary spatial penalty does not inherit those results. [Rawlings, Mayne and Diehl, chapters 1–2](https://sites.engineering.ucsb.edu/~jbraw/mpc/MPC-book-2nd-edition-6th-printing.pdf), [Caltech terminal-cost discussion, section 3.2](https://www.cds.caltech.edu/~murray/preprints/mur%2B03-sec.pdf).

**Application here:** the field cannot distinguish a ship stopped beside a rock from one at the same point moving toward it at 25 m/s. A shorter horizon exposes more of this omitted dynamics. A candidate direction is to combine geometric route guidance with a cheap dynamically simulated continuation, using the existing running costs. This is a hypothesis for a small prototype, not a proposed full-state grid or a stability claim.

## What does research say about maximizing speed with zero collisions?

MPCC optimizes progress along a route while controlling deviation from it. MPCC++ adds spatial constraints and a feasible terminal set, addressing the tradeoff created when collision avoidance is only another objective weight. Its results are for a specified drone track and model. [MPCC++ sections III–IV](https://arxiv.org/html/2403.17551v1).

The Dynamic Window Approach admits only velocity choices from which its robot can stop before an obstacle, subject to its acceleration limits. [Fox, Burgard and Thrun, sections 3–4](https://publications.ri.cmu.edu/storage/publications/pub_files/pub1/fox_dieter_1997_1/fox_dieter_1997_1.pdf).

**Application here:** goalward maximum-velocity tracking penalizes necessary sideways motion; measured fine-grid weight-3 steering saved 1.3854 field-cost points but added 1.4864 running-cost points. Route progress may align better with traversal intent. Separately, an endpoint can be collision-free while its velocity leaves no safe continuation. Test braking/escape feasibility with our ship model; do not transplant circular car trajectories or claim safety from finite penalties. Neither a route objective nor an admissibility rule has been implemented.

## Should the field choose a route explicitly?

Topology-driven MPC separates distinct evasive choices, optimizes each, and selects a feasible trajectory. It also addresses consistency across planning cycles. Its full framework is more machinery than our next experiment requires. [de Groot et al., sections IV and VII](https://arxiv.org/html/2401.06021v2).

**Application here:** preserve an explicit left/right alternative long enough to evaluate it. This may help with local optimizer history without blind periodic resets. Use two simple route seeds as a diagnostic first. A persistent route choice is a design change, not something established by the current scalar field contract.

## Is bilinear smoothing enough to fix grid geometry?

Field D* incorporates interpolation into the path-cost update, allowing continuous headings rather than only eight grid directions. Its paper also discusses cases where interpolation across different obstacle routes is inaccurate. [Ferguson and Stentz, sections 2–4](https://publications.ri.cmu.edu/storage/publications/pub_files/pub4/ferguson_david_2005_4/ferguson_david_2005_4.pdf).

**Application here:** smoothing the final Dijkstra samples is not equivalent to computing a continuous-distance field. Correct cell coverage and useful local scale remain necessary. A continuous-cost prototype should first face the one-circle analytic reference and offset-rock cases. Field D* is a relevant comparison, not a selected replacement.

## What are the next smallest discriminating experiments?

1. **Inspect route averaging at one captured solve.** Keep sampled controls, costs, sensing and state fixed. Compare each elite's swept clearance with the emitted average. Compare best feasible sample and averages within left/right groups only if an actual averaging failure is found.
2. **Measure missing terminal dynamics.** Hold endpoint position fixed and vary incoming velocity and yaw. Compare the identical geometric field value with the cost and clearance of longer model continuations. This tests what a short horizon needs the terminal term to know.
3. **Test route guidance separately from speed choice.** Use the same single rock and two explicit passing alternatives. Compare the current goalward velocity objective with progress along the selected route, at fixed sensing and spatial resolution. This deliberately changes the diagnostic objective; production sentences remain frozen.

Keep each test at one snapshot or a few seconds. Revisit 1.2 s versus 1.7 s only after these measurements explain which responsibility can safely leave the MPC horizon. The 0.7 s experiments bracket a failure mechanism; they do not select a production horizon.

## What implementation should we keep now?

Keep the independently verified all-results gather optimization. It preserves geometry and control traces and removes repeated nearest-prefix selection. Keep production horizon, field resolution, weights and sampling policy unchanged pending the experiments above. The successful 1 m/weight-30 single-rock results are useful evidence, not a broadly validated setting.
