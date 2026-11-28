# Line sheet

Every companion, crew and enemy line lives in one file:
`src/Asteroids3D/Assets/Settings/Game/Companion/LineSheet.csv`. The game reads that file and nothing else. The owner ruled it the single canonical source on 2026-11-28 (#1653). A Claude Sheet, "Andromeda line sheet", mirrors it for editing on any device. The old "Companion and Enemy Lines" doc is retired.

## Columns

`ID, Category, Moment, Faction, Speaker, After, Line, Status, Notes`. The game reads ID, Moment, Faction, Speaker, After, Line and Status. Category and Notes are for people; the owner's grades live in Notes.

- **Status:** only `In game` rows load. `Later` waits for a feature the game doesn't have yet, and Notes says which. `Draft` is written but not signed off. `Cut` is retired, and the row stays.
- **Faction:** blank means any sector.
- **After:** blank opens the exchange. `any` follows any opening line from another speaker in the same moment and faction. An ID follows only that line. `either` can open, or follow an opening line from another speaker.
- **Pools:** rows that share a moment, faction and speaker are interchangeable, and the game picks one at random.
- **IDs** are never reused, even after a row is cut.
- **Moment** must name a line slot on `CompanionLines.asset`. A row naming an unknown moment, or a reply that can never play, fails the load, and the line tests catch it.

The file is plain CSV: a cell with a comma, a quote or a line break is wrapped in double quotes, and a quote inside is doubled. Any `.csv` change runs the game's tests before it lands; only `.md` and `doc/` changes skip them.

## Syncing with the Claude Sheet

"Sync the lines" means:
1. A session reads the sheet and diffs it against the CSV on main, row by row and matched by ID.
2. It shows the owner the changed rows: added, removed, and changed cells.
3. Sheet edits land as a one-file PR to the CSV after the line tests pass.
4. CSV changes that landed some other way (an agent's PR, a Muse or Neon pass the owner accepted) are written back to the sheet after they merge.

When the same row changed on both sides since the last sync, the session asks the owner which side wins. It never picks one quietly.

Line-writing taste and the tone rules live in the owner's graded notes and the `feedback-lore-line-taste` memory. They aren't repeated here.
