# Maintaining

Tasks for whoever administers the GitHub repository and the Azure subscription. Everyday work is in [CONTRIBUTING](CONTRIBUTING.md).

- [CI setup and maintenance](docs/administrators/CISetupAndMaintenance.md): rulesets on `main`, repository settings, setup of a new repository.
- [CD setup](docs/administrators/CDSetup.md): Azure identities, GitHub environments and secrets, first deployment, teardown.

## Why GitHub Flow

The project has one maintainer, so direct pushes to `main` would be enough. It still works like a team application: every change reaches `main` through a pull request with required checks. Git Flow (`develop`, release branches) is heavier and built for versioned releases, not for an app deployed from `main`.

## Why squash merge only

- `main` has one commit per pull request, titled as the pull request: easy to read and to revert.
- The intermediate commits of a branch stay visible in the closed pull request.
- Merge commits would keep them on `main`, which is useful only for long-lived branches.
- Rebase merging would put every intermediate commit on `main`, so each branch would need cleaning up before the merge.
