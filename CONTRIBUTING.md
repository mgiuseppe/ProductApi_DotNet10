# Contributing

## Tools

- [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker Desktop
- `dotnet-ef` for migrations: `dotnet tool install --global dotnet-ef`
- [GitHub CLI](https://cli.github.com/) for pull requests
- [Claude Code](https://claude.com/claude-code): commits go through its `/verify-and-commit` skill
- VS Code with C# Dev Kit and REST Client (for the `.http` files in `src/Api/TestsHttp`)

## Build and test

```bash
dotnet build ProductApi.slnx
dotnet format ProductApi.slnx --verify-no-changes
dotnet test tests/Domain.Tests
dotnet test tests/Application.Tests
dotnet test tests/Infrastructure.Tests      # needs Docker: SQL Server in Testcontainers
```

Where tests go:
- domain rules: `Domain.Tests`;
- command handlers, with mocked repositories: `Application.Tests`;
- repositories and query handlers, on a real SQL Server: `Infrastructure.Tests`.

One test class per tested class, names `Method_Condition_Result`, `// Arrange`, `// Act`, `// Assert`. Never call the real LLM: mock `IChatClient`. More in [Testing strategy](docs/TestingStrategy.md).

## Run

- **API with F5**, infrastructure in Docker: see the [README](README.md#run-it-locally).
- **Everything in Docker**, to check the container before a push: `docker compose --profile app up --build -d` (API on http://localhost:8080).
- **Ports**: copy `.env.example` to `.env`. If you change the SQL Server, MongoDB or OTLP port, update `src/Api/appsettings.Development.json` too.
- **OpenAI key**: user-secrets with F5, `OPENAI_API_KEY` in `.env` for the container.
- **Stop**: `docker compose --profile app down` (add `-v` to delete the data).

## Migrations

```bash
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

A migration can be changed until it is merged into `main`; after that, a mistake is fixed with a new migration ([ADR-0009](docs/adr/0009-migrations-forward-only-after-merge.md)). Risky operations are listed in [EF Core](docs/EFCore.md#changes-that-need-attention).

## API documentation

The OpenAPI document is generated from the XML comments of the endpoint methods.

- Every endpoint is documented: `<summary>`, `<param>` for its parameters, `<response>` for each status code.
- Document the contract (what the caller sends and gets back), not the implementation.
- `<remarks>` only for what the caller must know and does not fit in the summary.
- Endpoints are named `internal static` methods: the XML comments of lambdas and private methods do not reach the OpenAPI document.
- Domain and Application know nothing about HTTP.

## Workflow

GitHub Flow: `main` is protected, every change goes through a short-lived branch and a pull request.

```bash
git switch main && git pull
git switch -c feature/<short-name>          # or fix/, docs/, chore/
# work, then in Claude Code: /verify-and-commit (ask it to push and open the pull request)
gh pr merge --squash --delete-branch        # after reading the diff
```

In practice you ask Claude Code to start a task: it proposes branch names, creates the one you choose from an up-to-date `main`, and later commits, pushes and opens the pull request when you ask.

- When you ask Claude Code to commit, it runs the `/verify-and-commit` skill, which checks quality (format, build, tests and migrations).
- No pull request approval is required, because this is a solo project and GitHub does not let authors approve their own pull requests. Review means reading the diff and running the `architecture-reviewer` and `code-reviewer` agents.
- The CI jobs are required checks: a pull request cannot be merged until they pass.
- **Squash merge only**: one commit per pull request on `main`, titled as the pull request. So keep pull requests small, never reuse a merged branch, and put formatting-only changes in their own pull request.

Repository settings and rulesets: [MAINTAINING](MAINTAINING.md).

## Architecture decisions

Write an ADR in `docs/adr/` only for decisions that change the structure or are expensive to reverse: layers, persistence, messaging, transaction boundaries, external integrations, infrastructure. Workflow rules and details go in the topic documents.
