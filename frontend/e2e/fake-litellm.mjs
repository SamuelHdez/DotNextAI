// Fake LiteLLM proxy for the E2E suite: a deterministic OpenAI-compatible chat completions endpoint, so the browser
// tests exercise the real API, agent and OpenAI SDK without the real proxy, keys or costs. No dependencies.
//
//   node e2e/fake-litellm.mjs [port]      (default 4010)
//
// Behaviour, from the last user message:
//   "[e2e:unavailable]"  -> LiteLLM's real 400 "no healthy deployments" answer
//   "[e2e:slow]"         -> waits 1.5 s, then (when streaming) sends a word every 400 ms: the thinking state,
//                           the partial answer and Stop can be seen
//   "[e2e:drop]"         -> streaming only: sends the first word, then drops the connection
//   "what is my name"    -> finds "My name is X" in the history: "Your name is X." (proves the history arrives)
//   image parts          -> "I see N image(s) (types): <question>" when every image is a base64 data URI
//   anything else        -> "Echo: <question>"
// With "stream": true the answer is sent as OpenAI chat.completion.chunk Server-Sent Events, one word per chunk,
// ending with a usage chunk when stream_options.include_usage is set. Usage is estimated: input = characters of
// every message / 4 (so it grows with the history), output = words of the answer.
// POST /images/generations answers a 1×1 PNG in base64 with revised_prompt "Drawn: <prompt>" (or the outage of
// "[e2e:unavailable]"), and refuses response_format, which gpt-image models do not accept.
// GET /models (and /v1/models) lists two chat models plus non-chat ones, with LiteLLM's "mode"; answers name the
// model that was asked for, so the tests see which model answered.
// GET /model/info (and /v1/model/info) gives LiteLLM's prices per token (PRICES), so answers show a cost.
import { createServer } from "node:http";

const port = Number(process.argv[2] ?? process.env.FAKE_LITELLM_PORT ?? 4010);
const MODEL = "e2e-model";
const MODELS = [
  { id: "e2e-model", object: "model", owned_by: "openai", mode: "chat", max_input_tokens: 922000, max_output_tokens: 128000 },
  { id: "e2e-model-fast", object: "model", owned_by: "openai", mode: "chat", max_input_tokens: 128000, max_output_tokens: 16000 },
  { id: "e2e-embedding", object: "model", owned_by: "openai", mode: "embedding", max_input_tokens: 8192 },
  { id: "e2e-image", object: "model", owned_by: "openai", mode: "image_generation" },
];
// US dollars per token, as LiteLLM's model_info: $10 / $30 per million for e2e-model, $1 / $2 for e2e-model-fast.
const PRICES = {
  "e2e-model": { input_cost_per_token: 0.00001, output_cost_per_token: 0.00003 },
  "e2e-model-fast": { input_cost_per_token: 0.000001, output_cost_per_token: 0.000002 },
};

function textOf(content) {
  if (typeof content === "string") return content;
  if (Array.isArray(content)) return content.map((part) => part?.text ?? "").join("");
  return "";
}

function send(response, status, body) {
  response.writeHead(status, { "Content-Type": "application/json" });
  response.end(JSON.stringify(body));
}

function usageFor(messages, answer) {
  const prompt_tokens = Math.ceil(messages.reduce((sum, message) => sum + textOf(message.content).length, 0) / 4);
  const completion_tokens = answer.split(/\s+/).filter(Boolean).length;
  return { prompt_tokens, completion_tokens, total_tokens: prompt_tokens + completion_tokens };
}

function completion(text, model, usage) {
  return {
    id: `chatcmpl-e2e-${Date.now()}`,
    object: "chat.completion",
    created: Math.floor(Date.now() / 1000),
    model,
    choices: [{ index: 0, message: { role: "assistant", content: text }, finish_reason: "stop" }],
    usage,
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

function chunk(delta, model, finishReason = null) {
  return `data: ${JSON.stringify({
    id: "chatcmpl-e2e",
    object: "chat.completion.chunk",
    created: Math.floor(Date.now() / 1000),
    model,
    choices: [{ index: 0, delta, finish_reason: finishReason }],
  })}\n\n`;
}

const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const PIXEL_PNG = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

function generateImage(response, body) {
  const prompt = typeof body.prompt === "string" ? body.prompt : "";
  if (body.response_format !== undefined) {
    return send(response, 400, { error: { message: "Unknown parameter: 'response_format'.", type: "invalid_request_error" } });
  }
  if (prompt.includes("[e2e:unavailable]")) {
    return send(response, 400, {
      error: { message: `litellm.BadRequestError: You passed in model=${body.model}. There are no healthy deployments for this model` },
    });
  }
  return send(response, 200, {
    created: Math.floor(Date.now() / 1000),
    data: [{ b64_json: PIXEL_PNG, revised_prompt: `Drawn: ${prompt} (${body.size})` }],
    usage: { input_tokens: Math.ceil(prompt.length / 4), output_tokens: 4160, total_tokens: Math.ceil(prompt.length / 4) + 4160 },
  });
}

async function stream(response, text, model, { slow, drop, usage }) {
  response.writeHead(200, { "Content-Type": "text/event-stream", "Cache-Control": "no-cache", Connection: "keep-alive" });
  if (slow) await wait(1500);
  const words = text.split(/(?<=\s)/);
  for (const [index, word] of words.entries()) {
    response.write(chunk(index === 0 ? { role: "assistant", content: word } : { content: word }, model));
    if (drop) {
      // Let the first word reach the client, then break the connection mid-answer.
      await wait(200);
      response.socket?.destroy();
      return;
    }
    await wait(slow ? 400 : 15);
  }
  response.write(chunk({}, model, "stop"));
  if (usage) {
    response.write(`data: ${JSON.stringify({ id: "chatcmpl-e2e", object: "chat.completion.chunk", created: Math.floor(Date.now() / 1000), model, choices: [], usage })}\n\n`);
  }
  response.end("data: [DONE]\n\n");
}

const server = createServer((request, response) => {
  const path = (request.url ?? "").split("?")[0];
  if (request.method === "GET" && path === "/health") return send(response, 200, { status: "healthy" });

  if (request.method === "GET" && /^\/(v1\/)?models$/.test(path)) {
    if (!/^Bearer \S+$/.test(request.headers.authorization ?? "")) {
      return send(response, 401, { error: { message: "Authentication Error, no api key passed in." } });
    }
    return send(response, 200, { data: MODELS, object: "list" });
  }

  if (request.method === "GET" && /^\/(v1\/)?model\/info$/.test(path)) {
    if (!/^Bearer \S+$/.test(request.headers.authorization ?? "")) {
      return send(response, 401, { error: { message: "Authentication Error, no api key passed in." } });
    }
    const data = MODELS.map(({ id, mode }) => ({ model_name: id, litellm_params: { model: `openai/${id}` }, model_info: { mode, ...PRICES[id] } }));
    return send(response, 200, { data });
  }

  const isImages = request.method === "POST" && /^\/(v1\/)?images\/generations$/.test(path);
  if (!isImages && (request.method !== "POST" || !/^\/(v1\/)?chat\/completions$/.test(path))) {
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

    if (isImages) return generateImage(response, body);

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

    const slow = question.includes("[e2e:slow]");
    if (body.stream === true) {
      const answer = answerFor(messages);
      const usage = body.stream_options?.include_usage ? usageFor(messages, answer) : null;
      stream(response, answer, body.model ?? MODEL, { slow, drop: question.includes("[e2e:drop]"), usage }).catch(() => response.destroy());
      return;
    }
    const answer = answerFor(messages);
    setTimeout(() => send(response, 200, completion(answer, body.model ?? MODEL, usageFor(messages, answer))), slow ? 1500 : 0);
  });
});

server.listen(port, "127.0.0.1", () => console.log(`Fake LiteLLM listening on http://127.0.0.1:${port}`));
