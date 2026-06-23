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
    fileName: string;
    hash?: string | null;
    signal?: AbortSignal;
    onProgress?: (bytesUploaded: number) => void;
  }): Promise<void> => {
    if (!options.hash) {
      throw new Error("Chunk hash is required for raw chunk uploads.");
    }

    await httpClient.post("chunks/raw", options.blob, {
      params: { hash: options.hash },
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
