# Ship art stage 5: UV

## Inputs

- The source under the geometry lock: `ship_lock --mode verify` passes.
- `texture_size` and `uv_density_max_ratio` in `ship.json`.

## Commands

From `art/tools/ship/README.md`:

- `ship_lock --mode verify` before any UV work.
- Unwrap on the `PaintUV` UV layer.
- `ship_uv` reports texel density per part and fails past `uv_density_max_ratio`.

## Done

- `ship_uv` passes.
- `ship_lock --mode verify` still passes: unwrapping left the geometry untouched.
- The source is committed; the `ship_uv` report is on `evidence/<name>` under `uv/`.

## Owner's gate

The owner approves the layout in chat, with the `ship_uv` report beside it.

## Forbidden moves

- **Changing geometry while unwrapping.** A shape change is a reopen with the owner's
  approval (`SKILL.md` → Gates).
- **Packing at the eye.** Crimson shipped one part at 7.7 times the density of the rest
  because packing ignored object scale; `ship_uv` is the judge.
