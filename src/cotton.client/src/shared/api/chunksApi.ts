import { httpClient } from "./httpClient";

export const chunksApi = {
  exists: async (hash: string): Promise<boolean> => {
    const response = await httpClient.get(`/chunks/${encodeURIComponent(hash)}/exists`, {
      validateStatus: (status) => status === 200 || status === 404,
    });
    return response.status === 200;
  },

  uploadChunk: async (options: { blob: Blob; fileName: string; hash?: string | null }): Promise<void> => {
    const form = new FormData();

    // ASP.NET IFormFile binding expects a "file" field.
    form.append("file", options.blob, options.fileName);

    // When hash validation is disabled, omit the field entirely (-> null server-side).
    if (options.hash != null) {
      form.append("hash", options.hash);
    }

    await httpClient.post("/chunks", form, {
      headers: {
        // Let the browser/axios set correct multipart boundary.
        "Content-Type": "multipart/form-data",
      },
    });
  },
};
