import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { HeicDecoderClient } from "./HeicDecoderClient";
import type { HeicDecodeResponse } from "./HeicDecodeResponse";

class DecoderWorker {
  static instances: DecoderWorker[] = [];
  onmessage: ((event: MessageEvent<HeicDecodeResponse>) => void) | null = null;
  onerror: ((event: ErrorEvent) => void) | null = null;
  onmessageerror: (() => void) | null = null;
  postMessage = vi.fn();
  terminate = vi.fn();

  constructor() {
    DecoderWorker.instances.push(this);
  }

  respond(response: HeicDecodeResponse): void {
    this.onmessage?.(new MessageEvent("message", { data: response }));
  }
}

const createInput = () => {
  const bytes = new ArrayBuffer(4);
  return {
    bytes,
    blob: Object.assign(new Blob(), { arrayBuffer: async () => bytes }),
  };
};

describe("HeicDecoderClient", () => {
  beforeEach(() => {
    DecoderWorker.instances = [];
    vi.stubGlobal("Worker", DecoderWorker);
  });

  afterEach(() => vi.unstubAllGlobals());

  it("loads lazily and correlates concurrent results using a single worker", async () => {
    const decoder = new HeicDecoderClient();
    expect(DecoderWorker.instances).toHaveLength(0);
    const input = createInput();
    const first = decoder.decode(input.blob);
    const second = decoder.decode(createInput().blob);
    await vi.waitFor(() =>
      expect(DecoderWorker.instances[0].postMessage).toHaveBeenCalledTimes(2),
    );
    expect(DecoderWorker.instances).toHaveLength(1);
    const worker = DecoderWorker.instances[0];
    expect(worker.postMessage).toHaveBeenNthCalledWith(
      1,
      {
        requestId: 1,
        bytes: input.bytes,
        runtimeUrl: new URL(
          "/heic/_framework/dotnet.js",
          window.location.origin,
        ).href,
      },
      [input.bytes],
    );
    const image = { width: 1, height: 1, pixels: new ArrayBuffer(4) };
    worker.respond({ type: "decoded", requestId: 2, image });
    worker.respond({
      type: "error",
      requestId: 1,
      message: "Invalid HEIC data",
    });
    await expect(second).resolves.toEqual(image);
    await expect(first).rejects.toThrow("Invalid HEIC data");
    expect(worker.terminate).not.toHaveBeenCalled();
  });

  it("rejects all pending decodes when the worker fails", async () => {
    const decoder = new HeicDecoderClient();
    const first = decoder.decode(createInput().blob);
    const second = decoder.decode(createInput().blob);
    const rejected = Promise.all([
      expect(first).rejects.toThrow("Runtime failed"),
      expect(second).rejects.toThrow("Runtime failed"),
    ]);
    await vi.waitFor(() =>
      expect(DecoderWorker.instances[0].postMessage).toHaveBeenCalledTimes(2),
    );
    const worker = DecoderWorker.instances[0];
    worker.onerror?.(
      new ErrorEvent("error", { message: "Runtime failed", cancelable: true }),
    );
    await rejected;
    expect(worker.terminate).toHaveBeenCalledOnce();
    await expect(decoder.decode(createInput().blob)).rejects.toThrow(
      "Runtime failed",
    );
    expect(DecoderWorker.instances).toHaveLength(1);
  });

  it("rejects an unreadable response without leaving a pending request", async () => {
    const decoder = new HeicDecoderClient();
    const pending = decoder.decode(createInput().blob);
    const rejected = expect(pending).rejects.toThrow("could not be read");
    await vi.waitFor(() => expect(DecoderWorker.instances).toHaveLength(1));
    DecoderWorker.instances[0].onmessageerror?.();
    await rejected;
  });
});
