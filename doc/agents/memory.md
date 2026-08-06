# Agent memory

> STATUS: living — branch-triggered reference for memory reads/writes, especially from worktree agents; pointed at from `AGENTS.md`.

This repo is backed by a persistent, file-based agent memory — durable facts,
decisions, and the *why/how* behind tracker issues. It lives **outside** the repo,
in the primary session's memory directory:

`C:\Users\amind\.claude\projects\D--amind-git-astronomical-home\memory\`

- **`MEMORY.md`** in that directory is the index: one line per memory, loaded
  into the primary session's context automatically at session start. Each fact
  is its own `.md` file (with frontmatter) linked from the index; add a fact by
  writing the file and appending a one-line pointer to `MEMORY.md`. Prefer
  updating an existing file over creating a near-duplicate.
- **The directory is keyed to the working-tree path** (`D--amind-git-astronomical-home`).
  An agent running in an `agent-N` worktree resolves a *different* memory dir
  and will **not** auto-load the primary session's memory. Such agents must read
  and write the **absolute path above**, not their own memory dir. This is the
  same reason the active-work ledger
  (`…/memory/active_work_ledger.md`) is always referenced by absolute path — see
  `AGENTS.md` → "Cross-agent work ledger".
- **Three tracking surfaces, don't conflate them:** agent memory holds the
  durable *why/how*; **GitHub Issues** hold title-plus-link backlog /
  status items; the **active-work ledger** holds live, right-now claims/locks.
  Issues and ledger rows link out to the memory file that carries their
  detail.
