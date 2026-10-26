import { reportClientError } from "../utils/clientDiagnostics";
import type { HeicDecodeRequest } from "./HeicDecodeRequest";
import type { HeicDecodeResponse } from "./HeicDecodeResponse";
import type { HeicImage } from "./HeicImage";

interface PendingDecode {
  resolve: (image: HeicImage) => void;
  reject: (error: Error) => void;
}

export class HeicDecoderClient {
  private worker: Worker | undefined;
  private failure: Error | undefined;
  private requestId = 0;
  private readonly pending = new Map<number, PendingDecode>();

  async decode(blob: Blob): Promise<HeicImage> {
    if (this.failure) {
      throw this.failure;
    }
    const bytes = await blob.arrayBuffer();
    if (this.failure) {
      throw this.failure;
    }
    const worker = this.getWorker();
    const requestId = ++this.requestId;
    const request: HeicDecodeRequest = {
      requestId,
      bytes,
      runtimeUrl: new URL(
        `${import.meta.env.BASE_URL}heic/_framework/dotnet.js`,
        window.location.origin,
      ).href,
    };
    return new Promise((resolve, reject) => {
      this.pending.set(requestId, { resolve, reject });
      try {
        worker.postMessage(request, [bytes]);
      } catch (error) {
        this.pending.delete(requestId);
        reportClientError("HEIC decode request failed.", error);
        reject(error);
      }
    });
  }

  private getWorker(): Worker {
    if (!this.worker) {
      this.worker = new Worker(new URL("./heic.worker.ts", import.meta.url), {
        type: "module",
      });
      this.worker.onmessage = (event: MessageEvent<HeicDecodeResponse>) => {
        const response = event.data;
        const request = this.pending.get(response.requestId);
        if (!request) {
          return;
        }
        this.pending.delete(response.requestId);
        switch (response.type) {
          case "decoded":
            request.resolve(response.image);
            return;
          case "error":
            reportClientError("HEIC image decoding failed.", response.message);
            request.reject(new Error(response.message));
            return;
          default: {
            const unexpected: never = response;
            const error = new Error(
              `Unexpected HEIC decoder response: ${String(unexpected)}`,
            );
            request.reject(error);
            this.fail(error);
          }
        }
      };
      this.worker.onerror = (event) => {
        event.preventDefault();
        this.fail(new Error(event.message || "HEIC decoder worker failed."));
      };
      this.worker.onmessageerror = () =>
        this.fail(new Error("HEIC decoder response could not be read."));
    }
    return this.worker;
  }

  private fail(error: Error): void {
    this.failure = error;
    reportClientError("HEIC decoder is unavailable.", error);
    this.worker?.terminate();
    for (const request of this.pending.values()) {
      request.reject(error);
    }
    this.pending.clear();
  }
}
