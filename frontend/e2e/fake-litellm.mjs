// Fake LiteLLM proxy for the E2E suite: a deterministic OpenAI-compatible chat completions endpoint, so the browser
// tests exercise the real API, agent and OpenAI SDK without the real proxy, keys or costs. No dependencies.
//
//   node e2e/fake-litellm.mjs [port]      (default 4010)
//
// Behaviour, from the last user message:
//   "[e2e:unavailable]"  -> LiteLLM's real 400 "no healthy deployments" answer
//   "[e2e:slow]"         -> answers after 1.5 s (to see the thinking indicator)
//   "what is my name"    -> finds "My name is X" in the history: "Your name is X." (proves the history arrives)
//   image parts          -> "I see N image(s) (types): <question>" when every image is a base64 data URI
//   anything else        -> "Echo: <question>"
import { createServer } from "node:http";

const port = Number(process.argv[2] ?? process.env.FAKE_LITELLM_PORT ?? 4010);
const MODEL = "e2e-model";

function textOf(content) {
  if (typeof content === "string") return content;
  if (Array.isArray(content)) return content.map((part) => part?.text ?? "").join("");
  return "";
}

function send(response, status, body) {
  response.writeHead(status, { "Content-Type": "application/json" });
  response.end(JSON.stringify(body));
}

function completion(text) {
  return {
    id: `chatcmpl-e2e-${Date.now()}`,
    object: "chat.completion",
    created: Math.floor(Date.now() / 1000),
    model: MODEL,
    choices: [{ index: 0, message: { role: "assistant", content: text }, finish_reason: "stop" }],
    usage: { prompt_tokens: 10, completion_tokens: 5, total_tokens: 15 },
  };
}

// Images arrive as OpenAI `image_url` parts with a data URI (step 06): "data:image/png;base64,...".
function imagesOf(content) {
  if (!Array.isArray(content)) return [];
  return content
    .filter((part) => part?.type === "image_url")
    .map((part) => /^data:(image\/[a-z]+);base64,[A-Za-z0-9+/]+=*$/.exec(part.image_url?.url ?? "")?.[1] ?? "invalid");
}

function answerFor(messages) {
  const userMessages = messages.filter((message) => message.role === "user").map((message) => textOf(message.content));
  const question = userMessages.at(-1) ?? "";
  const images = imagesOf(messages.filter((message) => message.role === "user").at(-1)?.content);
  if (images.length > 0) {
    return `I see ${images.length} image${images.length === 1 ? "" : "s"} (${images.join(", ")}): ${question}`;
  }
  if (/what is my name/i.test(question)) {
    const name = userMessages
      .slice(0, -1)
      .map((text) => /my name is ([\p{L}-]+)/iu.exec(text)?.[1])
      .findLast(Boolean);
    return name ? `Your name is ${name}.` : "I do not know your name yet.";
  }
  return `Echo: ${question}`;
}

const server = createServer((request, response) => {
  const path = (request.url ?? "").split("?")[0];
  if (request.method === "GET" && path === "/health") return send(response, 200, { status: "healthy" });

  if (request.method !== "POST" || !/^\/(v1\/)?chat\/completions$/.test(path)) {
    return send(response, 404, { error: { message: `No route for ${request.method} ${path}` } });
  }

  if (!/^Bearer \S+$/.test(request.headers.authorization ?? "")) {
    return send(response, 401, { error: { message: "Authentication Error, no api key passed in." } });
  }

  let raw = "";
  request.on("data", (chunk) => (raw += chunk));
  request.on("end", () => {
    let body;
    try {
      body = JSON.parse(raw);
    } catch {
      return send(response, 400, { error: { message: "Invalid JSON body." } });
    }

    const messages = Array.isArray(body.messages) ? body.messages : [];
    const question = textOf(messages.filter((message) => message.role === "user").at(-1)?.content);

    if (question.includes("[e2e:unavailable]")) {
      return send(response, 400, {
        error: {
          message: `litellm.BadRequestError: You passed in model=${body.model}. There are no healthy deployments for this model\n\nLiteLLM: model group '${body.model}' failed with the error above. No fallback was attempted.`,
          type: "invalid_request_error",
          param: null,
          code: "400",
        },
      });
    }

    const delay = question.includes("[e2e:slow]") ? 1500 : 0;
    setTimeout(() => send(response, 200, completion(answerFor(messages))), delay);
  });
});

server.listen(port, "127.0.0.1", () => console.log(`Fake LiteLLM listening on http://127.0.0.1:${port}`));
