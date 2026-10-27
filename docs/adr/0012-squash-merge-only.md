# 0012. Squash merge only

- Status: Accepted
- Date: 2026-10-27

## Context
With GitHub Flow ([0011](0011-github-flow.md)) every change reaches `main` through a short-lived branch and a pull request. GitHub offers three merge methods (merge commit, squash, rebase) and recommends allowing only one, so the history stays consistent. Much of the code is written with an AI agent, which produces many small intermediate commits ("fix test", "format", "address review") that say little on their own.

## Decision
Pull requests are merged with **squash merge only**; the other two methods are disabled in the repository settings and in the ruleset. One pull request becomes one commit on `main`, whose message is the pull request title and description. Branches are deleted after the merge.

## Consequences
- `main` reads as a list of changes, one per pull request, each with a title that says what changed.
- Undoing a change is a single `git revert` of its commit.
- **Lost**: the intermediate commits of the branch, with their authors and hashes. They stay visible in the closed pull request on GitHub, not in `git log`.
- **Pull requests must stay small.** A change that would deserve its own commits deserves its own pull request.
- **A branch is not reused after its merge**: further commits on it would bring back the squashed ones and repeat their conflicts (GitHub documents this). Start a new branch from `main`.
- **Formatting-only changes go in their own pull request**, so that the squash commit can be added to `.git-blame-ignore-revs`; mixed with other changes, it could not.
- The pull request title becomes the commit subject: short and imperative, like any commit message.

## Alternatives considered
- **Merge commit** (usually after rebasing the branch on `main`): keeps every commit of the branch in the history, grouped under a merge commit. Intermediate commits that do not build or pass the tests stay in the history, but off the main line: `git log --first-parent` shows only the merges, each one tested by the pull request. A valid choice when the intermediate commits are meaningful, for example in long-lived feature branches; here they are mostly the agent's small steps.
- **Rebase merge**: copies every commit of the branch onto `main`, with no merge commit to group them. Every intermediate commit becomes a state of the main line itself, so the commits should be cleaned up before each merge (fixups folded into the commit they fix), otherwise the main line contains states that do not build or pass the tests.
- **Allow all three**: inconsistent history, harder to read and to revert.
