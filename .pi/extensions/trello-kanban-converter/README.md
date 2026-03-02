# Trello ↔ Obsidian Kanban Converter (Pi extension)

Project-local Pi extension that converts:
- **Trello JSON export** → **obsidian-kanban** markdown board
- **obsidian-kanban** markdown board → **Trello-like JSON** (best-effort)

## Install / Load
This repo already contains the extension under:
- `.pi/extensions/trello-kanban-converter/index.ts`

In Pi interactive mode, run:
- `/reload`

## Tools

### `trello_to_kanban`
Parameters:
- `inputJsonPath` (string)
- `outputMdPath` (string)
- `includeArchived` (bool, default true): put closed cards / cards from closed lists under `## Archive`
- `includeChecklistItems` (bool, default true)
- `includeLabelsAsTags` (bool, default true): Trello labels become `#tags` on the card line

Example:
```text
Use trello_to_kanban with inputJsonPath: asteroids-final-project.json and outputMdPath: Engineering/Asteroids Board.md
```

### `kanban_to_trello`
Parameters:
- `inputMdPath` (string)
- `outputJsonPath` (string)
- `boardName` (optional string)
- `pretty` (bool, default true)

Note: IDs are generated; this is intended for content round-tripping, not updating an existing Trello board.

## Slash commands
- `/trello-to-kanban <input.json> <output.md>`
- `/kanban-to-trello <input.md> <output.json>`

(These just nudge you toward using the tools with the right parameters.)
