# Terminal app (CLI)

> **Status:** Step 01 (single question and answer) implemented on 2026-11-16. The terminal app is a permanent deliverable: it keeps working and grows with every backlog step, next to the web app.

`dotnextai` (project `backend/src/DotNextAI.Cli`) is the terminal front end of DotNextAI. It uses the same Application and Infrastructure services as the API, so a feature is written once and offered in both front ends.

## Decisions

| Topic | Decision | Date |
| ----- | -------- | ---- |
| Lifetime | The CLI is not a throw-away prototype for Phase 1. It stays in the repository, in CI and in the docs after the web app exists, and new backlog features are added to it where they make sense (see [roadmap](#roadmap)). | 2026-11-16 |
| Command parsing | `System.CommandLine` 2.0.x (the line that ships with .NET 10). 3.x belongs to .NET 11 and waits for the move to that line. | 2026-11-16 |
| Output streams | stdout carries only answers; prompts, warnings and errors go to stderr. Answers can be piped or redirected without noise. | 2026-11-16 |
| Language | English only, including System.CommandLine's own texts (help, parse errors): the process runs with the invariant culture. Multi-language applies to the web UI, not to the CLI (confirmed by the product owner). | 2026-11-16 |
| Encoding | UTF-8 without BOM for stdin and stdout, so accents survive in Windows terminals and in redirected output. | 2026-11-16 |
| Logs | Off by default, so they never mix with answers. When enabled (`Logging__LogLevel__Default=Information`), they go to stderr and never contain prompts or answers. | 2026-11-16 |
| Configuration | The LiteLLM settings come from the user-secrets shared with the API (`./scripts/dev.sh secrets`). The CLI runs in `Development` by default so it loads them; `DOTNET_ENVIRONMENT` overrides it. | 2026-11-16 |

## Commands

### `dotnextai ask [question...]` — one question, one answer (step 01)

| Input | Behavior |
| ----- | -------- |
| `dotnextai ask "What is .NET?"` | Sends the question and prints the answer. |
| `dotnextai ask What is .NET?` | Same: unquoted words are joined with single spaces. |
| `echo "…" \| dotnextai ask`, `dotnextai ask < question.txt` | With no question argument and redirected stdin, the whole of stdin is the question (multi-line allowed). |
| `dotnextai ask` in a terminal | Writes `Question: ` to stderr and reads one line. |

Rules:

- The question is trimmed. Empty questions and questions longer than 20,000 characters are rejected before any model call ([chat rules](feature-llm-chat.md#rules)).
- Configuration is checked **before** the question is read, so nobody types a question that cannot be sent.
- No history: each run is independent. Conversation history arrives with step 03.
- Ctrl+C cancels the call in progress.

### `dotnextai` (no command)

Interactive mode. In step 01 it behaves like `dotnextai ask` with no argument: one question from the prompt, one answer, exit. From step 03 it becomes the multi-turn chat loop.

### `dotnextai --help`, `dotnextai <command> --help`, `dotnextai --version`

Provided by System.CommandLine.

## Messages

All on stderr.

| Situation | Message | Exit code |
| --------- | ------- | --------- |
| Proxy not configured | `The LiteLLM proxy is not configured. Run ./scripts/dev.sh secrets first.` | 3 |
| Empty question | `The question is empty.` | 2 |
| Question too long | `The question is too long (N characters; the maximum is 20,000).` | 2 |
| Unknown command or option | System.CommandLine's error (e.g. `Unrecognized command or argument 'x'.`) followed by `Run 'dotnextai --help' for usage.` | 2 |
| Model call failed | `Error: <reason>` (see [failures](feature-llm-chat.md#failures)), then a hint line when there is one, see below | 1 |
| Answer cut at the maximum output length | The answer on stdout, then `Warning: the answer was cut off because it reached the maximum length.` | 0 |
| Ctrl+C | `Cancelled.` | 130 |

Hints after `Error:`:

| Failure | Hint |
| ------- | ---- |
| `Unauthorized`, `NotConfigured` | `Check the settings with ./scripts/dev.sh secrets.` |
| `ModelNotFound` | `Check Llm:DefaultModel with ./scripts/dev.sh secrets (see the proxy's model list).` |
| `ModelUnavailable` | `Try again later, or pick another model with ./scripts/dev.sh secrets.` |
| `Unavailable`, `Timeout` | `Check the network (VPN) and Llm:Endpoint, then try again.` |

## Exit codes

| Code | Meaning |
| ---- | ------- |
| 0 | Answer printed. |
| 1 | The model call failed. |
| 2 | Usage error: unknown command or option, empty or too long question. |
| 3 | The LiteLLM proxy is not configured. |
| 130 | Cancelled with Ctrl+C. |

## Running it

```bash
./scripts/dev.sh cli ask "What is .NET?"     # through dotnet run
./scripts/dev.sh cli                         # interactive
dotnet run --project backend/src/DotNextAI.Cli -- ask "What is .NET?"
```

## Roadmap

How the later backlog steps are planned to reach the CLI. Each step's spec confirms or changes this when the step starts.

| Step | In the CLI |
| ---- | ---------- |
| 02 Agent Framework | `ask` keeps its behavior; the call moves to an Agent Framework agent behind `IChatService`. |
| 03 Chat history | `dotnextai` (no command) becomes a multi-turn chat loop with in-memory history. |
| 06 Image input | Attach image files by path. |
| 07 Streaming | Print the answer as it is generated. |
| 08 Model selector | `--model` option and a command that lists the proxy's chat models. |
| 09 Image generation | Command that generates an image and saves it to a file. |
| 10 Token metrics | Tokens per call and per conversation. |

## Verification

- `CliApplicationTests` (unit, fake `IChatService`): quoted and unquoted questions, stdin, interactive prompt, no command, empty and too-long questions, not configured (checked before reading), failures with their hints and exit codes, truncation warning, Ctrl+C (130), `--help`, unknown command or option (2).
- `ChatRulesTests`: trimming and the length limit.
- Manual: `./scripts/dev.sh cli ask "…"` against the proxy (see [todo.md](../tasks/todo.md)).
