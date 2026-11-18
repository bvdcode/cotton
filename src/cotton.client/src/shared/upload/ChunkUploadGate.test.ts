import { describe, expect, it, vi } from "vitest";
import { ChunkUploadGate } from "./ChunkUploadGate";

describe("ChunkUploadGate", () => {
  it("shares capacity and admits waiting uploads in order", async () => {
    const gate = new ChunkUploadGate(2);
    const signal = new AbortController().signal;
    const releases: (() => void)[] = [];
    const starts: number[] = [];
    const uploads = Array.from({ length: 6 }, (_, index) =>
      gate.run(async () => {
        starts.push(index);
        await new Promise<void>((resolve) => releases.push(resolve));
      }, signal),
    );

    await vi.waitFor(() => expect(starts).toEqual([0, 1]));
    for (let index = 0; index < uploads.length; index += 1) {
      releases[index]();
      await uploads[index];
      await vi.waitFor(() =>
        expect(starts).toEqual(
          Array.from(
            { length: Math.min(index + 3, uploads.length) },
            (_, i) => i,
          ),
        ),
      );
    }
  });

  it("removes a cancelled waiter without taking capacity from the next upload", async () => {
    const gate = new ChunkUploadGate(1);
    const controller = new AbortController();
    const signal = new AbortController().signal;
    let release = () => {};
    const first = gate.run(
      () =>
        new Promise<void>((resolve) => {
          release = resolve;
        }),
      signal,
    );
    const cancelledOperation = vi.fn(async () => {});
    const cancelled = gate.run(cancelledOperation, controller.signal);
    const cancelledResult = expect(cancelled).rejects.toThrow(
      "Upload was cancelled.",
    );
    const nextOperation = vi.fn(async () => {});
    const next = gate.run(nextOperation, signal);

    controller.abort();
    await cancelledResult;
    expect(cancelledOperation).not.toHaveBeenCalled();
    expect(nextOperation).not.toHaveBeenCalled();
    release();
    await Promise.all([first, next]);
    expect(nextOperation).toHaveBeenCalledOnce();
  });

  it("releases capacity when an upload fails", async () => {
    const gate = new ChunkUploadGate(1);
    const signal = new AbortController().signal;
    await expect(
      gate.run(async () => {
        throw new Error("Upload failed.");
      }, signal),
    ).rejects.toThrow("Upload failed.");
    await expect(gate.run(async () => 42, signal)).resolves.toBe(42);
  });
});
