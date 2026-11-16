# Terminal app (CLI)

> **Status:** Step 06 (image input) implemented on 2026-11-16, on top of steps 01 and 03 (single question and answer, multi-turn chat). The terminal app is a permanent deliverable: it keeps working and grows with every backlog step, next to the web app.

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
| Chat history | Kept in the CLI process by `ChatConversation` (Application) and sent with every question. It is lost when the chat ends; nothing is written to disk. | 2026-11-16 |
| Leaving the chat | `/exit`, `/quit` or end of input (Ctrl+D; Ctrl+Z then Enter on Windows) exit with 0. Ctrl+C cancels and exits with 130, as in `ask`. | 2026-11-16 |
| Chat commands | Lines starting with `/` are commands: `/new`, `/help`, `/exit`, `/quit` (case-insensitive). They are never sent to the model. | 2026-11-16 |
| Conversation limit | 50 questions per conversation. At the limit the chat asks for `/new` instead of dropping old turns silently. | 2026-11-16 |
| One line, one question | In the chat every line is a question; multi-line questions go through `ask` (stdin). | 2026-11-16 |
| Configuration | The LiteLLM settings come from the user-secrets shared with the API (`./scripts/dev.sh secrets`). The CLI runs in `Development` by default so it loads them; `DOTNET_ENVIRONMENT` overrides it. | 2026-11-16 |

## Commands

### `dotnextai ask [question...]` — one question, one answer (step 01)

| Input | Behavior |
| ----- | -------- |
| `dotnextai ask "What is .NET?"` | Sends the question and prints the answer. |
| `dotnextai ask What is .NET?` | Same: unquoted words are joined with single spaces. |
| `echo "…" \| dotnextai ask`, `dotnextai ask < question.txt` | With no question argument and redirected stdin, the whole of stdin is the question (multi-line allowed). |
| `dotnextai ask` in a terminal | Writes `Question: ` to stderr and reads one line. |
| `dotnextai ask --image cat.png -i dog.jpg "What is in these pictures?"` | Attaches up to 4 image files (step 06). Each one is loaded and checked before the question is read. |

Rules:

- The question is trimmed. Empty questions and questions longer than 20,000 characters are rejected before any model call ([chat rules](feature-llm-chat.md#rules)).
- Configuration is checked **before** the question is read, so nobody types a question that cannot be sent.
- No history: each `ask` run is independent. For a conversation, use the chat.
- Images (`--image`, `-i`, repeatable): PNG, JPEG, WebP or GIF, recognised by their bytes whatever the extension, at most 4 MB each (the size is checked before reading the file).
- Ctrl+C cancels the call in progress.

### `dotnextai` or `dotnextai chat` — multi-turn chat (step 03)

The default command. Until step 03 `dotnextai` alone asked one question, like `ask`.

In a terminal:

```text
DotNextAI chat. Type /help for commands, /exit or Ctrl+D to quit.
You: My name is Ana.
Nice to meet you, Ana!

You: What is my name?
Your name is Ana.

You: /exit
```

The banner, the `You: ` prompts and the blank line after each answer go to stderr; the answers go to stdout.

With redirected stdin (`dotnextai chat < questions.txt`), there is no banner and no prompt. Every non-empty line is a question asked after the previous ones, and only the answers are written.

Rules:

- Every question is sent with the previous questions and answers of the conversation ([chat service](feature-llm-chat.md#conversation-history)).
- Empty lines are ignored.
- A question that fails (model error, too long) is reported and **not** added to the history. The chat goes on, and the question can be asked again.
- At 50 questions, further questions are refused with the limit message until `/new`.
- Attached images go with the next question only. After an answer they are used up; when the question fails they stay attached for the retry. The history keeps the text.
- Configuration is checked before the banner; when it is missing the chat exits with 3.

| Chat command | Effect |
| ------------ | ------ |
| `/image <path>` | Loads an image and attaches it to the **next** question: `Attached cat.png (image/png, 120 KB). It goes with your next question.` Up to 4 per question. |
| `/images` | Lists the images waiting for the next question, or `No images attached.` |
| `/new` | Forgets the conversation and the attached images, and writes `Started a new conversation.` |
| `/help` | Lists the chat commands. |
| `/exit`, `/quit` | Ends the chat (exit code 0). |
| any other `/…` | `Unknown command '/…'. Type /help for the list.` |

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
| Chat: conversation at the limit | `The conversation reached the limit of 50 questions. Type /new to start a new one.` (the chat goes on) | — |
| Chat: unknown chat command | `Unknown command '/x'. Type /help for the list.` (the chat goes on) | — |
| Chat: model error, empty or too long question | Same texts as above; the chat goes on | — |
| Image not found | `Image not found: <path>` | 2 (`ask`), — (chat) |
| Not an image | `<name> is not a PNG, JPEG, WebP or GIF image.` | 2 (`ask`), — (chat) |
| Image too large | `<name> is too large (N MB; the maximum is 4 MB).` | 2 (`ask`), — (chat) |
| Too many images | `ask`: `Too many images (N; the maximum is 4 per question).` Chat: `You can attach up to 4 images to one question.` | 2 (`ask`), — (chat) |
| `/image` without a path | `Usage: /image <path to a PNG, JPEG, WebP or GIF file>` | — |

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
| 0 | Answer printed (`ask`), or the chat ended with `/exit`, `/quit` or end of input. |
| 1 | The model call failed. |
| 2 | Usage error: unknown command or option; for `ask`, an empty or too long question. |
| 3 | The LiteLLM proxy is not configured. |
| 130 | Cancelled with Ctrl+C. |

## Running it

```bash
./scripts/dev.sh cli ask "What is .NET?"     # through dotnet run
./scripts/dev.sh cli                         # chat
dotnet run --project backend/src/DotNextAI.Cli -- ask "What is .NET?"
```

## Roadmap

How the later backlog steps are planned to reach the CLI. Each step's spec confirms or changes this when the step starts.

| Step | In the CLI |
| ---- | ---------- |
| 02 Agent Framework | **Done 2026-11-16.** `ask` keeps its behavior; the call goes through an Agent Framework agent behind `IChatService`. |
| 03 Chat history | **Done 2026-11-16.** `dotnextai` / `dotnextai chat`: multi-turn chat with in-memory history, `/new`, `/help`, `/exit`. |
| 06 Image input | **Done 2026-11-16.** `ask --image <path>` and `/image <path>` in the chat. |
| 07 Streaming | Print the answer as it is generated. |
| 08 Model selector | `--model` option and a command that lists the proxy's chat models. |
| 09 Image generation | Command that generates an image and saves it to a file. |
| 10 Token metrics | Tokens per call and per conversation. |

## Verification

- `ChatCommandTests` (unit, fake `IChatService`): history sent with each question, stdout with only the answers, banner and prompts, `/exit` `/quit` `/new` `/help`, unknown commands, empty lines, failed question not recorded, too-long question, 50-question limit, not configured (3), Ctrl+C (130).
- `CliApplicationTests` (unit, fake `IChatService`): `ask` with quoted and unquoted questions, stdin, interactive prompt, no history, empty and too-long questions, not configured (checked before reading), failures with their hints and exit codes, truncation warning, Ctrl+C (130), `--help`, unknown command or option (2).
- `ImageCommandTests`:
  - `ask --image`: images detected by content, missing file, non-image, too large, too many.
  - Chat: `/image` for the next question only, `/images`, `/new` discards, kept after a failure, usage without a path, limit, non-image.
- `ChatRulesTests`: trimming and the length limit.
- Manual: `./scripts/dev.sh cli ask "…"` against the proxy (see [todo.md](../tasks/todo.md)).
