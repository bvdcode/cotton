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
    const form = new FormData();

    // ASP.NET IFormFile binding expects a "file" field.
    form.append("file", options.blob, options.fileName);

    // When hash validation is disabled, omit the field entirely (-> null server-side).
    if (options.hash != null) {
      form.append("hash", options.hash);
    }

    await httpClient.post("chunks", form, {
      signal: options.signal,
      headers: {
        "Content-Type": "multipart/form-data",
      },
      onUploadProgress: (event) => {
        if (!options.onProgress) {
          return;
        }

        const total = event.total ?? 0;
        const bytesUploaded =
          total > 0
            ? (event.loaded / total) * options.blob.size
            : event.loaded;

        options.onProgress(
          Math.floor(Math.max(0, Math.min(options.blob.size, bytesUploaded))),
        );
      },
    });
  },
};
