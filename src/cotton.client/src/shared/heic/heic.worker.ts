/// <reference lib="webworker" />

import type { HeicDecodeRequest } from "./HeicDecodeRequest";
import type { HeicDecodeResponse } from "./HeicDecodeResponse";
import type { HeicDecodedPixels, HeicRuntimeModule } from "./HeicRuntime";

declare const self: DedicatedWorkerGlobalScope;

type Decoder = (bytes: Uint8Array) => HeicDecodedPixels;
let decoderPromise: Promise<Decoder> | undefined;

const createDecoder = async (runtimeUrl: string): Promise<Decoder> => {
  const module: HeicRuntimeModule = await import(/* @vite-ignore */ runtimeUrl);
  const runtime = await module.dotnet.create();
  runtime.setModuleImports("heic", {
    createImage: (width, height, pixels) => ({ width, height, pixels }),
  });
  const exports = await runtime.getAssemblyExports(
    runtime.getConfig().mainAssemblyName,
  );
  return exports.Cotton.Heic.Browser.HeicDecoder.Decode;
};

self.addEventListener("message", async (event: MessageEvent<HeicDecodeRequest>) => {
  const request = event.data;
  let response: HeicDecodeResponse;
  try {
    decoderPromise ??= createDecoder(request.runtimeUrl);
    const decode = await decoderPromise;
    const result = decode(new Uint8Array(request.bytes));
    const pixels = new Uint8Array(result.pixels).buffer;
    response = {
      type: "decoded",
      requestId: request.requestId,
      image: { width: result.width, height: result.height, pixels },
    };
    self.postMessage(response, [pixels]);
  } catch (error) {
    response = {
      type: "error",
      requestId: request.requestId,
      message: error instanceof Error ? error.message : String(error),
    };
    self.postMessage(response);
  }
});
