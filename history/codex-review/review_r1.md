**1. Image 2: the three biggest failures**

- **The frame owns the silhouette.** Seven, eight or nine sides barely changes the result: every weapon becomes “a broken circle containing something.” At game scale, the perimeter consumes the contrast and space needed to identify the weapon. Changing the polygon cannot fix this hierarchy.
- **The marks share too much structure.** Lasers, Turbo and Railgun are diagonal stripe bundles; Missiles and Rippers are triangles on that same diagonal. Their differences live inside nearly identical envelopes. Railgun especially needs a dominant central mass, not three strokes of comparable importance.
- **The interruptions are mechanically distributed.** Every polygon edge gets the same treatment, regardless of the enclosed mark. Those gaps create texture rather than useful separation. Meanwhile, the marks crowd or cross the perimeter, producing accidental joins. The solid version merely fills those problems into a badge.

**2. What Image 1 gets right**

Its advantage is primarily **composition and empty space**, not elaborate terminal shapes. Several terminals look like ordinary flat cuts; replacing square caps alone will not rescue Image 2.

Rules worth coding:

- Use one steep slash direction, approximately **65° above horizontal**.
- Give neighboring strokes unequal lengths: approximately **1 : 0.8 : 0.6**, rather than equal lengths translated sideways.
- Stagger starts and ends independently. Do not center every stroke on a common midpoint.
- Make clear gaps at least approximately one stroke width; at your smallest size, aim for **two actual pixels between important features**.
- Use enclosures selectively to express behavior. Charge’s opposing angles suggest containment; the missile outline suggests direction. Neither receives an unrelated perimeter.
- Concentrate asymmetry in **one decision per glyph**: a shorter neighboring stroke, a displaced tick, or an open side. Randomly perturbing every primitive would destroy the precision.

**3. One scheme: a shared rear corner**

Use **two short, connected edges of a sheared parallelogram**, positioned behind and left of every mark. This provides an actual bounding cue while leaving the muzzle and most of the perimeter open. Keep this corner identical across the set. Its job is recognition of the family; the large interior mark identifies the weapon.

Concrete construction:

- **16 × 16 grid.** Use local coordinates `(u,v)`, with `v` pointing toward the muzzle. Convert to screen coordinates with  
  `x = 2 + u + 0.5v`, `y = 14 − v`.
- This gives a **63.4° slash axis**.
- Main strokes: **1.0 unit perpendicular thickness**. Shared corner: **0.8 unit**. Flat off-white, no glow.
- Shared corner: polyline **`(0,2) → (0,0) → (3.5,0)`**.
- Beam terminals and projectile heels: **horizontal screen cuts**. Brackets and radial ticks: flat butt ends. No rounded caps.
- Below, `B(u,a,b,w)` means a solid slash centered on constant `u`, extending from `v=a` to `v=b`, with perpendicular thickness `w`.

Primitives for each weapon, **in addition to the shared corner**:

- **Lasers:** `B(2.5,2.8,11,1)` and `B(5,3.4,10,1)`. Two substantial slashes; unequal spans and offset heels.
- **Turbo Lasers:** the same two slashes, plus `B(7.5,4.2,9.2,1)`. The added stroke is visibly shorter. Preserve the first two exactly so the relationship is explicit.
- **Charge Lasers:** `B(4.5,4.4,8.6,1)`. Add inward-pointing bracket polylines **`(1,4) → (2,6.5) → (1,9)`** and **`(8,4) → (7,6.5) → (8,9)`**, width 1. These express compression around one beam.
- **Railgun:** `B(5,2.5,10.5,2.4)`, flanked by `B(2,2.5,10.5,0.8)` and `B(8,2.5,10.5,0.8)`. All three share terminal heights. The central stripe must read as **one heavy shot between guides**.
- **Missiles:** solid triangle **`(3.3,8)`, `(6.3,8)`, `(4.8,11.5)`**. Trail: `B(4.8,5.2,6.5,1)` and `B(4.8,2.7,4,1)`. A narrow head, detached from its trail, avoids the broad cursor silhouette.
- **Rippers:** three identical pointed pentagons centered at `u=4.5`, starting at `v=2.5, 6, 9.5`. Each is **2 units wide × 2.5 high**, with parallel sides for the first 1.5 units and a pointed final unit. Flat heels, equal ammunition shapes, no lateral scatter.
- **Concussion:** screen-space circle centered at transformed `(4,6)`, diameter **1.8**. Five radial ticks, width 1, spanning radii **2.5–4.1**, spaced 72° apart; align one with the slash axis. No surrounding ring.

**4. First failures at 22 px**

**Railgun:** its guides can merge with the core and recreate Turbo. At 22 px, snap the core to roughly **3 px**, guides to **1 px**, and preserve the intervening gaps—even if the guides must move outward slightly.

**Rippers:** their pointed noses can disappear, leaving three trail dashes. Give each projectile a **minimum 3 px body width** and a clearly resolved pointed nose. Maintain equal sizes and spacing. Their distinction from Missiles must be **three substantial rounds versus one head with a thin trail**.