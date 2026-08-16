import { describe, expect, it, vi } from "vitest";
import { BoundedByteQueue } from "./BoundedByteQueue";

describe("BoundedByteQueue", () => {
  it("waits until both item and byte capacity are available", async () => {
    const queue = new BoundedByteQueue<string>(2, 4);
    await queue.enqueue("first", 2);
    await queue.enqueue("second", 2);

    const thirdEnqueued = vi.fn();
    const third = queue.enqueue("third", 2).then(thirdEnqueued);
    await new Promise((resolve) => {
      globalThis.setTimeout(resolve, 0);
    });
    expect(thirdEnqueued).not.toHaveBeenCalled();

    await expect(queue.dequeue()).resolves.toBe("first");
    await third;
    expect(thirdEnqueued).toHaveBeenCalledOnce();
    await expect(queue.dequeue()).resolves.toBe("second");
    await expect(queue.dequeue()).resolves.toBe("third");
  });

  it("drains queued values before completing consumers", async () => {
    const queue = new BoundedByteQueue<string>(2, 4);
    await queue.enqueue("value", 1);
    queue.close();

    await expect(queue.dequeue()).resolves.toBe("value");
    await expect(queue.dequeue()).resolves.toBeNull();
  });

  it("unblocks producers and consumers when the pipeline fails", async () => {
    const producerQueue = new BoundedByteQueue<string>(1, 1);
    await producerQueue.enqueue("first", 1);
    const blockedProducer = producerQueue.enqueue("second", 1);
    const failure = new Error("failed");
    producerQueue.fail(failure);

    await expect(blockedProducer).rejects.toBe(failure);
    await expect(producerQueue.dequeue()).rejects.toBe(failure);

    const consumerQueue = new BoundedByteQueue<string>(1, 1);
    const blockedConsumer = consumerQueue.dequeue();
    consumerQueue.fail(failure);
    await expect(blockedConsumer).rejects.toBe(failure);
  });
});
