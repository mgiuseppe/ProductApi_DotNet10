# Maintaining

Tasks for whoever administers the GitHub repository; everyday work (branches, pull requests, checks) is in [CONTRIBUTING](CONTRIBUTING.md), the decisions behind the CI in [CICD](docs/CICD.md).

## Rulesets

The rules on `main` are versioned as JSON in `.github/rulesets/` and applied with its [sync.sh](.github/rulesets/sync.sh).

GitHub does not read these files by itself (unlike `.github/workflows/` and `.github/dependabot.yml`): **after changing a file, run `sync.sh`**. Changes made in the UI are not written back to the files, and the next run overwrites them. The script needs bash (on Windows, Git Bash) and `gh` logged in as a repository admin.

Several rulesets on the same branch add up: all their rules apply.

| File | What it does |
| --- | --- |
| [main-require-pr.json](.github/rulesets/main-require-pr.json) | Changes reach `main` only through a pull request (0 approvals, squash merge only). No force push, no deletion, no bypass, administrators included. |
| [main-quality-gates.json](.github/rulesets/main-quality-gates.json) | Merge only when the required status checks are green (`build-and-test`, `images`, `bicep`, `dependency-review`, jobs of `ci.yml`) and CodeQL reports no high or critical security alert. |

## Repository settings

Set by hand in the GitHub UI: they apply to the whole repository, not to a branch, so a ruleset cannot express them.

**Settings → General → Pull Requests**
- Only **Allow squash merging**: uncheck merge commits and rebase merging. The `main-require-pr` ruleset already refuses them; this removes their buttons.
- **Automatically delete head branches**: the branch goes away once its pull request is merged.

**Settings → Advanced Security**
- **Private vulnerability reporting**: people report a vulnerability privately instead of in a public issue.
- **Dependabot alerts**: a dependency has a known vulnerability.
- **Dependabot security updates**: a pull request that fixes it, as soon as the alert opens. Not the weekly updates of `.github/dependabot.yml`, which cover every dependency, vulnerable or not.
- **CodeQL analysis → Default**: static analysis on every pull request and weekly, no workflow file.
- **Secret scanning**: an alert when a known secret format is found in the repository; you should revoke the secret, then close the alert.
- **Push protection**: refuses a push that contains a known secret format.

## Setup a new repository

In this order:

1. The repository settings above. First, because the `main-quality-gates` ruleset needs CodeQL results.
2. The `main-require-pr` ruleset.

   ```bash
   ./.github/rulesets/sync.sh main-require-pr.json
   ```

3. The `main-quality-gates` ruleset, **only once the CI jobs it requires are in `ci.yml` on `main`**: a required check is matched by job name, and a pull request whose workflow lacks the job waits for a status that is never reported.

   ```bash
   ./.github/rulesets/sync.sh main-quality-gates.json
   ```

`./.github/rulesets/sync.sh` without arguments applies every file. It also lists the rulesets in the repository that no file describes (`unmanaged`): they keep applying next to the others, so delete them in the UI after review.

## Continuous delivery

The one-time Azure and GitHub environment setup for CD is in [SETUP_CICD_ONETIME](SETUP_CICD_ONETIME.md).
