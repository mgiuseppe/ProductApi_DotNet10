# 0007. IChatClient without a port of our own

- Status: Accepted
- Date: 2026-11-07

## Context
`GET /orders/{id}/summary` asks an LLM to summarize an order and its history. Clean Architecture suggests a port for external services, but `IChatClient` (Microsoft.Extensions.AI) is already a provider-neutral abstraction.

## Decision
- The query handler depends on `IChatClient` directly; Application references only `Microsoft.Extensions.AI.Abstractions`.
- The OpenAI SDK and the client registration stay in Infrastructure.
- Prompts and `ChatOptions` live in the handler; order data is wrapped in `<data>` tags against prompt injection.

## Consequences
- Changing provider only changes the registration.
- Prompts are versioned and reviewed with the code.
- Application knows an LLM is used; tests mock `IChatClient`.

## Alternatives considered
- **Use-case port** (`IOrderSummarizer`): hides the LLM and controls which fields leave the system. Worth it if a non-LLM implementation is expected.
- **Generic port** (`AnswerAsync(prompt)`): adds a type without hiding anything more than `IChatClient`.
- **Prompts loaded from configuration or a database**, editable by an administrator: more complexity and out of scope. The parameters still come from code, and a prompt changed outside code can drift between environments. A prompt affects output quality, cost and latency, so it needs the same review as code.
