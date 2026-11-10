import type { SharedUploadContext } from "../upload/types";
import { httpClient } from "./httpClient";

export const chunksApi = {
  exists: async (hash: string, signal?: AbortSignal): Promise<boolean> => {
    const response = await httpClient.get<boolean>(
      `chunks/${encodeURIComponent(hash)}/exists`,
      {
        signal,
        validateStatus: (status) => status === 200 || status === 404,
      },
    );
    if (response.status === 404) {
      return false;
    }
    return response.data;
  },

  uploadChunk: async (options: {
    blob: Blob;
    sharedUpload?: SharedUploadContext & { nodeId: string };
    fileName: string;
    hash?: string | null;
    signal?: AbortSignal;
    onProgress?: (bytesUploaded: number) => void;
  }): Promise<void> => {
    if (!options.hash) {
      throw new Error("Chunk hash is required for raw chunk uploads.");
    }

    const access = options.sharedUpload;
    const url = access
      ? `layouts/shared/${encodeURIComponent(access.token)}/chunks/raw`
      : "chunks/raw";
    await httpClient.post(url, options.blob, {
      params: {
        hash: options.hash,
        nodeId: access?.nodeId,
        sessionId: access?.sessionId,
      },
      signal: options.signal,
      headers: {
        "Content-Type": "application/octet-stream",
      },
      onUploadProgress: (event) => {
        if (!options.onProgress) {
          return;
        }

        const total = event.total ?? 0;
        const bytesUploaded =
          total > 0 ? (event.loaded / total) * options.blob.size : event.loaded;

        options.onProgress(
          Math.floor(Math.max(0, Math.min(options.blob.size, bytesUploaded))),
        );
      },
    });
  },
};
