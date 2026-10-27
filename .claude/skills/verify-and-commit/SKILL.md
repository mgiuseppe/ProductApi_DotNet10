---
name: verify-and-commit
description: Run the quality gate of ProductApi (format, build, unit tests, integration tests) and commit only if everything passes; optionally push the branch and open a pull request. Use whenever the user asks to commit, push, "ship" or "check before pushing" in this repository.
---

# Verify and commit

The gate described in `docs/CICD.md`, run locally so that what passes here passes in CI. Never commit or push with a failing step, and never use `--no-verify`.

## Steps

1. **Look at the change**: `git status` and `git diff`. If the change touches layers, persistence, messaging, transaction boundaries or external integrations, check that `docs/adr/` has an ADR for it and that `docs/ARCHITECTURE.md` is updated. If not, stop and propose the ADR first.
2. **Format**: `dotnet format ProductApi.slnx --verify-no-changes`. If it fails, show the files, run `dotnet format ProductApi.slnx`, show the resulting diff and include it in the commit only if the user agrees. Formatting of untouched files goes in a separate commit.
3. **Build**: `dotnet build ProductApi.slnx -warnaserror`. A new warning is a failure.
4. **Unit tests**: `dotnet test tests/Domain.Tests` and `dotnet test tests/Application.Tests`.
5. **Integration tests** (always before a push, before a commit when Infrastructure or a query handler changed): check Docker with `docker info`; if it is not running, ask the user to start Docker Desktop instead of skipping. Then `dotnet test tests/Infrastructure.Tests`.
6. **Commit**: never on `main` (GitHub Flow, ADR-0011). The branch is created by the user before starting the work. If the current branch is `main`, stop: tell the user the work should have started on a branch and ask for its name (`feature/`, `fix/`, `docs/`, `chore/`), never choose it yourself; create it with `git switch -c <name>` only after the answer. Never reuse a branch that was already merged. Stage only the files that belong to the change (no `bin/`, `obj/`, `.env`, secrets). Message: short imperative subject, then a body with the why when it is not obvious. Show the message before committing.
7. **Migration checks** (before every push; rules in `docs/EFCore.md`, section Migrations). Run `git fetch origin` first.
   - **Shared migrations untouched** (blocks): `git diff --name-status origin/main...HEAD -- src/Infrastructure/Migrations/` must show only `A` (added) files, plus `M` on `AppDbContextModelSnapshot.cs`. Any other `M`, `D` or `R` means a pushed migration was edited: stop and explain that the fix is a new migration.
   - **No forgotten migration** (blocks): `dotnet ef migrations has-pending-model-changes --project src/Infrastructure --startup-project src/Api` must report no changes.
   - **Changes that need attention** (information only, never blocks): in the migrations added since `origin/main` (excluding `*.Designer.cs`), search for `DropColumn`, `DropTable`, `RenameColumn`, `RenameTable`, `AlterColumn`, `unique: true`, `AddUniqueConstraint`, `AddForeignKey`, `AddCheckConstraint`, `migrationBuilder.Sql`, `AddColumn` with `nullable: false`, `CreateIndex` on an existing table. Also flag changes to `OrderHistoryEntriesConfiguration.cs` or `OrderHistoryEntries.cs` (MongoDB document shape). For each hit, quote the line and the risk from the table in `docs/EFCore.md` in the report. Point out explicitly a `DropColumn` + `AddColumn` pair on the same table, which usually means a rename scaffolded with data loss. This project does not require expand/contract (ADR-0010): do not ask for confirmation.
8. **Push and pull request**: only if the user asked for it in this request. Never push `main`. `git push -u origin <branch>`, then open the pull request with `gh pr create --base main`: the title is the future squash commit subject (short, imperative), the body lists the changes and the checks run. Report the pull request URL. Do not merge unless the user asks; when merging, use `gh pr merge --squash --delete-branch`.

## Report

End with a table: step, result (pass / fail / skipped and why), and the commit hash if one was created. If a step failed, show the relevant output and stop: do not try to fix code beyond formatting without asking.
