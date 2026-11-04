# CI setup and maintenance

For whoever administers the GitHub repository: the rules on `main` and the repository settings the CI relies on. Everyday work (branches, pull requests, checks) is in [CONTRIBUTING](../../CONTRIBUTING.md), the decisions behind the CI in [CICD](../CICD.md).

## Rulesets

The rules on `main` are versioned as JSON in `.github/rulesets/`. Several rulesets on the same branch add up: all their rules apply.

| File | What it does |
| --- | --- |
| [main-require-pr.json](../../.github/rulesets/main-require-pr.json) | Changes reach `main` only through a pull request (0 approvals, squash merge only). No force push, no deletion, no bypass, administrators included. |
| [main-quality-gates.json](../../.github/rulesets/main-quality-gates.json) | Merge only when the required status checks are green (`build-and-test`, `images`, `bicep`, `dependency-review`, jobs of `ci.yml`) and CodeQL reports no high or critical security alert. |

### Applying rulesets

GitHub does not read these files by itself (unlike `.github/workflows/` and `.github/dependabot.yml`): **after changing a file, apply it**, in one of two ways.

**Script:** [sync.sh](../../.github/rulesets/sync.sh) creates or updates each ruleset by name. It needs bash (on Windows, Git Bash) and `gh` logged in as a repository admin.

```bash
./.github/rulesets/sync.sh                          # every file
./.github/rulesets/sync.sh main-require-pr.json     # only the given file(s)
```

It also lists the rulesets in the repository that no file describes (`unmanaged`): they keep applying next to the others, so delete them in the UI after review.

**GitHub UI:**

1. Settings → Rules → Rulesets.
2. New ruleset → Import a ruleset, and choose the JSON file.
3. Check that enforcement is **Active**, then Create.

The import always creates a new ruleset: to replace an existing one, delete it first.

The JSON files are the source of truth: apply them as they are. If you change a ruleset in the UI, export it (Export ruleset) and replace its JSON file in the repository, otherwise the next apply, by script or by import, overwrites the change.

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

Setup in this order:

1. The repository settings above. First, because the `main-quality-gates` ruleset needs CodeQL results.
2. The `main-require-pr` ruleset ([Applying rulesets](#applying-rulesets)).
3. The `main-quality-gates` ruleset, **only once the CI jobs it requires are in `ci.yml` on `main`**: a required check is matched by job name, and a pull request whose workflow lacks the job waits for a status that is never reported.
