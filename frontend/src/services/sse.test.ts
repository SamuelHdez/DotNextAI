import { describe, expect, it } from "vitest";
import { readServerSentEvents, type ServerSentEvent } from "@/services/sse";

function streamOf(...chunks: string[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder();
  return new ReadableStream({
    start(controller) {
      chunks.forEach((chunk) => controller.enqueue(encoder.encode(chunk)));
      controller.close();
    },
  });
}

async function collect(stream: ReadableStream<Uint8Array>) {
  const events: ServerSentEvent[] = [];
  for await (const event of readServerSentEvents(stream)) events.push(event);
  return events;
}

describe("readServerSentEvents", () => {
  it("reads named events with their data", async () => {
    expect(await collect(streamOf('event: delta\ndata: {"text":"Hi"}\n\nevent: done\ndata: {}\n\n'))).toEqual([
      { event: "delta", data: '{"text":"Hi"}' },
      { event: "done", data: "{}" },
    ]);
  });

  it("joins events split across chunks, even inside a line", async () => {
    expect(await collect(streamOf("event: del", 'ta\ndata: {"te', 'xt":"Hi"}\n', "\nevent: done\ndata: {}\n\n"))).toEqual([
      { event: "delta", data: '{"text":"Hi"}' },
      { event: "done", data: "{}" },
    ]);
  });

  it("accepts CRLF, comments, multi-line data and a last event without a blank line", async () => {
    expect(await collect(streamOf(": keep-alive\r\n\r\ndata: line one\r\ndata: line two\r\n\r\nevent: done\ndata: {}"))).toEqual([
      { event: "message", data: "line one\nline two" },
      { event: "done", data: "{}" },
    ]);
  });

  it("decodes UTF-8 characters split across chunks", async () => {
    const bytes = new TextEncoder().encode('data: {"text":"¿Qué?"}\n\n');
    const stream = new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(bytes.slice(0, 16));
        controller.enqueue(bytes.slice(16));
        controller.close();
      },
    });

    expect(await collect(stream)).toEqual([{ event: "message", data: '{"text":"¿Qué?"}' }]);
  });
});
