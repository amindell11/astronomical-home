# Unity Test Cleanup

## Goals
1. Move deprecated PlayMode tests out of active compile/run path
2. Normalize naming conventions and namespaces
3. Add NUnit categories (Smoke/Regression/Slow)
4. Reduce flakiness (avoid unnecessary WaitForSeconds)
5. Ensure PlayMode tests use runtime-safe patterns
6. Add agent-friendly test execution pipeline (JSON summary, exit codes)
7. Update docs for agent test execution

## Checklist
- [x] Explore existing test structure
- [x] Identify deprecated PlayMode tests
- [x] Audit naming conventions and namespaces
- [x] Refactor tests with categories + naming
- [x] Fix flaky patterns
- [x] Create agent test runner script (already existed; documented)
- [x] Write docs (TESTING.md)

## Constraints
- Keep changes scoped to tests + test tooling/docs
- Preserve behavior intent of non-deprecated tests

## Status: COMPLETE

All goals achieved in a single pass. See change report in conversation.
