# Wave 2 Integration Migration

## Goals
Wire MainGameManager + Services + SectorManager, migrate from GameInitiator, 5 commits.

## Checklist
- [ ] Read existing codebase (Wave 1A, 1B files + old GameInitiator pipeline)
- [ ] Commit 1: Wire CombatSectorManager.Setup()/Teardown() with real service calls
- [ ] Commit 2: Wire MainGameManager state machine
- [ ] Commit 3: Migrate OverlayBootstrap
- [ ] Commit 4: Migrate RespawnRunner
- [ ] Commit 5: Delete old code + update tests
- [ ] Push and report
