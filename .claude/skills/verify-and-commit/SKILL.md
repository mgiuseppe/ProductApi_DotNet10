---
name: verify-and-commit
description: Run the quality gate of ProductApi (format, build, unit tests, integration tests) and commit only if everything passes; optionally push. Use whenever the user asks to commit, push, "ship" or "check before pushing" in this repository.
---

# Verify and commit

The gate described in `docs/CICD.md`, run locally so that what passes here passes in CI. Never commit or push with a failing step, and never use `--no-verify`.

## Steps

1. **Look at the change**: `git status` and `git diff`. If the change touches layers, persistence, messaging, transaction boundaries or external integrations, check that `docs/adr/` has an ADR for it and that `docs/ARCHITECTURE.md` is updated. If not, stop and propose the ADR first.
2. **Format**: `dotnet format ProductApi.slnx --verify-no-changes`. If it fails, show the files, run `dotnet format ProductApi.slnx`, show the resulting diff and include it in the commit only if the user agrees. Formatting of untouched files goes in a separate commit.
3. **Build**: `dotnet build ProductApi.slnx -warnaserror`. A new warning is a failure.
4. **Unit tests**: `dotnet test tests/Domain.Tests` and `dotnet test tests/Application.Tests`.
5. **Integration tests** (always before a push, before a commit when Infrastructure or a query handler changed): check Docker with `docker info`; if it is not running, ask the user to start Docker Desktop instead of skipping. Then `dotnet test tests/Infrastructure.Tests`.
6. **Commit**: stage only the files that belong to the change (no `bin/`, `obj/`, `.env`, secrets). Message: short imperative subject, then a body with the why when it is not obvious. Show the message before committing.
7. **Push**: only if the user asked for it in this request. Report branch and result.

## Report

End with a table: step, result (pass / fail / skipped and why), and the commit hash if one was created. If a step failed, show the relevant output and stop: do not try to fix code beyond formatting without asking.
