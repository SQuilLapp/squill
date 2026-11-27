# P21-92 - AI assistant (1/5): provider contract, settings and key storage

Status: todo
Depends on: none (the owner wants the AI assistant **last**: start it only after every other P21 card the owner picked)

## Goal
The foundation of an optional, provider-agnostic AI assistant: a contract, two HTTP implementations, opt-in settings, and the API key in the secret store. No UI, no prompt building.

Exists today: nothing for AI; `ISecretStore` + `SecretKeys`; `AppSettings`; JSON through source-generated contexts; no outbound HTTP in the app except the update check (Velopack, opt-in).

## Design decisions (final)
- `Squill.Platform/Assistant`: `IAssistantProvider.CompleteAsync(AssistantRequest, CancellationToken) -> AssistantReply` (system text, messages, max output tokens; reply text and token usage when given). Streaming is not required for the first version.
- Implementations over `HttpClient` with source-generated JSON: `OpenAiCompatibleProvider` (chat completions shape: covers OpenAI, Azure OpenAI, Mistral, and local servers such as Ollama or LM Studio through a base URL) and `AnthropicProvider` (Messages API shape). Model name is a free text setting (no hard-coded model list).
- `AssistantSettings` in `AppSettings`: `Enabled` (default **false**), provider kind, base URL, model, `SendSampleRows` (default **false**), `MaxSchemaTables` (default 50). The key in `SecretKeys.AssistantKey(providerKind)`; never in `settings.json`, never logged.
- No call happens unless `Enabled` and a user action asks for it (recipe 6). Local providers (`localhost` base URL) are labelled "local" in the UI.

## Out of scope
- Context building and privacy preview (P21-93), view-model (P21-94), heads (P21-95, P21-96).

## Tasks
- [ ] Tests with a fake `HttpMessageHandler`: request JSON shape per provider (golden), error mapping (401, 429, 5xx, timeout), key read from the secret store, nothing sent when disabled.
- [ ] Check the Native AOT publish of both heads stays at baseline (HttpClient + source-generated JSON).

## Acceptance criteria
- Recipe 2 common acceptance; AOT publish baseline.

## Risks
- Provider API shapes change: keep each implementation small and covered by golden tests; the base URL is configurable.

## Open points (defaults)
- Owner: which providers to ship first (default: OpenAI-compatible, which also covers local Ollama, and Anthropic).

## Suggested commit message
Add an opt-in AI assistant provider contract with OpenAI-compatible and Anthropic implementations
