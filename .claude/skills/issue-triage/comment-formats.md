# Triage comment formats

The comments a triage run posts for queued verdicts and one-short readiness,
and the on-event triage's note. Verdict vocabulary: `SKILL.md` § Verdicts.

## Bench proposal

```
Bench proposal <date> — reopen when <condition>
Evidence: <path | PR | comment>
Branch: bench/<topic> | none
Apply: gh issue close <N> --comment "Benched <date> — reopen when <condition>"
```

## Park proposal

```
Park proposal <date> — <why>
Evidence: <path | PR | comment>
Branch: park/<topic> | none
Apply: gh issue close <N> --reason "not planned" --comment "Parked <date> — <why>"
```

## Repri proposal

```
Repri proposal <date>: pri:<from> → pri:<to>
Evidence: <path | PR | comment>
Apply: gh issue edit <N> --remove-label pri:<from> --add-label pri:<to>
```

The apply line's board Status change follows `doc/agents/issue-tracker.md`
§ Projects board sync (the new `pri:*` label's option).

## Duplicate close

Queued by a `duplicate-of #M` verdict.

```
Apply: gh issue close <N> --reason "not planned" --comment "Duplicate of #M"
```

## Readiness proposal

The build-scope block a build session restates as its Step-1 scope once the
user applies the label (#617 *Ruled: the label as scope confirmation*).

```
Ready proposal <date>
Scope: <end-to-end, 1–3 lines>
Acceptance: <observable>
Approach: <one line naming the seam>
Size: S (<100 changed lines) | M (<300) | L (over the anti-churn bar)
Needs Unity: none | batch tests | editor
Blocked by: none | #N
Apply: gh issue edit <N> --add-label ready-for-agent
```

## One-short question

Readiness `one-short`: the issue is one decision short of buildable.

```
Question <date>: <one line>
Options: <a> · <b> · <c>
Recommendation: <one>
Evidence: <path | PR | comment>
```

## On-event note

The on-event triage's one comment per issue (`on-event.md`), rendered by
`scripts/on_event_triage.sh` from the verdict fields and edited in place on
later runs by its hidden marker. Only the lines with a finding appear; no
findings and no prior note means no comment.

```
<!-- on-event-triage -->
On-event triage <date>
Retry of #N — <closed title>
Premise not in the tree — <path:line>
Dead pointer: `<path>` → `<replacement>` | no replacement found
In flight (assigned)
```

A later clean run rewrites a prior note as:

```
<!-- on-event-triage -->
On-event triage <date> — earlier findings resolved.
```
