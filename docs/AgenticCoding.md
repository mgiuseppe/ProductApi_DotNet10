# Agentic coding

The project is developed with [Claude Code](https://claude.com/claude-code). It writes most of the code and the docs; the author decides, reviews every diff and owns every change. Working on the project means working with it: the rules and checks below are part of the workflow, not an option.

## What is in the repository

| File | Role |
|---|---|
| [CLAUDE.md](../CLAUDE.md) | Project rules, loaded in every session: layers, conventions, tests, workflow |
| [.claude/agents/architecture-reviewer.md](../.claude/agents/architecture-reviewer.md) | Read-only review against the layer rules and the ADRs |
| [.claude/agents/code-reviewer.md](../.claude/agents/code-reviewer.md) | Read-only review for clean code, race conditions, transactions and API documentation |
| [.claude/skills/verify-and-commit](../.claude/skills/verify-and-commit/SKILL.md) | Quality gate before every commit, migration checks before a push, then push and pull request on request |
| [.claude/settings.json](../.claude/settings.json) | Project settings: background sessions edit this checkout instead of a separate worktree. To run several independent agents in parallel, give each its own worktree, or they overwrite each other's changes |

The rules live in the repository, not in anyone's personal memory or settings, so every clone gets them.

## Workflow

1. Ask Claude Code to start a task: it proposes branch names and creates the one you choose from an up-to-date `main`.
2. Get the work done:
   - a complex task: if the goal is not clear yet, brainstorm first and let it ask you questions; then ask for a plan before it writes code;
   - a simple task: just say what to do;
   - an architectural change comes with its ADR, before or with the code ([decision records](adr/README.md));
   - keep the tests up to date with every change: they certify the intended behavior, and they are the gate the agent has to pass;
   - after a non-trivial change it runs the two reviewers: it fixes bugs and clear problems by itself, and asks you about anything that is a matter of judgment. You can also ask for a review of a diff, a branch or a pull request, mention an agent with `@`, or ask it to compare the code with the practices of reference repositories.
3. You read the diff and ask to commit: `/verify-and-commit` runs format, build, tests and migration checks, and commits only if everything passes.
4. When you ask, it pushes and opens the pull request. CI repeats the checks; you merge with squash after reading the diff.

## What keeps it reliable

- **Local and CI checks overlap, and CI decides.** Before the commit the skill runs what the `build-and-test` job runs (format, build, tests, migration checks), plus two judgment calls only an agent can make: whether the change needs an ADR (it stops if one is missing) and which new migrations are risky (information only). CI also builds and scans the images, lints the Bicep and reviews the dependencies. The CI jobs are required checks: a step skipped by the agent still blocks the merge.
- **Reviewers report, the agent fixes.** The reviewers are read-only; the agent fixes what they find, asks you about judgment calls, and every fix shows up in the diff.
- **You decide** branch names, ADRs, merges and anything that leaves the machine.
- **Instructions are not enforcement.** CLAUDE.md asks for the reviewers after non-trivial changes, but nothing forces it: CI and reading the diff are the real gates.

## References

- [Claude Code best practices](https://code.claude.com/docs/en/best-practices)
