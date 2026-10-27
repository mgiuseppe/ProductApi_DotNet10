# 0007. IChatClient without a port of our own

- Status: Accepted
- Date: 2026-10-16

## Context
`GET /orders/{id}/summary` asks an LLM for a short summary of an order and its history. Clean Architecture suggests hiding external services behind a port, but `IChatClient` (Microsoft.Extensions.AI) is already a vendor-neutral abstraction.

## Decision
`SummarizeOrderQueryHandler` depends directly on `IChatClient`. Application references only `Microsoft.Extensions.AI.Abstractions`; the OpenAI SDK, the client registration and the OpenTelemetry middleware stay in Infrastructure. Prompts and `ChatOptions` (`Temperature = 0.2`, `MaxOutputTokens = 500`) live in the handler. Order data is wrapped in `<data>` tags against prompt injection. The client is built on first use, so the API starts without an API key.

## Consequences
- Changing provider only changes the registration in `Infrastructure/DependencyInjection.cs`.
- Prompts are versioned, reviewed and tested with the code.
- Application knows that an LLM is used, and the whole `Order` is serialized into the prompt, so domain changes change the prompt.
- Tests mock `IChatClient`; summary quality is not evaluated.

## Alternatives considered
- **Use-case port** (`IOrderSummarizer` taking a DTO): Application no longer knows about LLMs and the DTO controls which fields leave the system. Worth it if a non-LLM implementation is expected.
- **Generic port** (`AnswerAsync(systemPrompt, userPrompt)`): tried first and rejected; it adds a type without hiding anything `IChatClient` does not already hide.
- **Externalized prompts**: only if prompts must change without a deploy. Details in [AI](../AI.md).
