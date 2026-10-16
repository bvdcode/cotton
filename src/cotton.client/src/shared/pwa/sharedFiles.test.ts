// @vitest-environment node
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  getSharedFiles,
  saveSharedFiles,
  uploadSharedFiles,
} from "./sharedFiles";
import { receiveSharedFiles } from "./receiveSharedFiles";

const entries = new Map<string, Response>();
const cache = {
  keys: async () => [...entries.keys()].map((url) => new Request(url)),
  match: async (input: string | Request) =>
    entries.get(typeof input === "string" ? input : input.url)?.clone(),
  put: async (url: string, response: Response) => {
    entries.set(url, response.clone());
  },
  delete: async (input: string | Request) =>
    entries.delete(typeof input === "string" ? input : input.url),
};
const origin = "https://cotton.example";

describe("shared files", () => {
  beforeEach(() => {
    entries.clear();
    let previous: Promise<void> = Promise.resolve();
    vi.stubGlobal("location", { origin });
    vi.stubGlobal("caches", { open: async () => cache });
    vi.stubGlobal("navigator", {
      locks: {
        request: <T>(_name: string, callback: () => Promise<T>): Promise<T> => {
          const result = previous.then(callback);
          previous = result.then(
            () => {},
            () => {},
          );
          return result;
        },
      },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("preserves multiple files, names, types, empty files and bytes", async () => {
    const files = [
      new File([new Uint8Array([0, 1, 255])], "Фото.png", {
        type: "image/png",
      }),
      new File([], "empty.txt", { type: "text/plain" }),
    ];
    const id = await saveSharedFiles(files);
    const upload = vi.fn(async (received: File[]) => {
      expect(received.map((file) => [file.name, file.type, file.size])).toEqual(
        files.map((file) => [file.name, file.type, file.size]),
      );
      expect(new Uint8Array(await received[0].arrayBuffer())).toEqual(
        new Uint8Array([0, 1, 255]),
      );
    });

    expect(await uploadSharedFiles(id, upload)).toBe(true);
    expect(upload).toHaveBeenCalledOnce();
    expect(entries.size).toBe(0);
  });

  it("hands a share to only one consumer even when two windows open it", async () => {
    const id = await saveSharedFiles([new File(["hello"], "hello.txt")]);
    const upload = vi.fn(async () => {});

    expect(
      await Promise.all([
        uploadSharedFiles(id, upload),
        uploadSharedFiles(id, upload),
      ]),
    ).toEqual([true, false]);
    expect(upload).toHaveBeenCalledOnce();
  });

  it("keeps files when preparing the upload fails so reloading can retry", async () => {
    const id = await saveSharedFiles([new File(["hello"], "hello.txt")]);
    await expect(
      uploadSharedFiles(id, async () => {
        throw new Error("offline");
      }),
    ).rejects.toThrow("offline");
    expect(entries.size).toBe(1);
    expect(await uploadSharedFiles(id, async () => {})).toBe(true);
  });

  it("does not read another share or retain expired abandoned files", async () => {
    const id = await saveSharedFiles([new File(["old"], "old.txt")]);
    const upload = vi.fn(async () => {});
    expect(await uploadSharedFiles("missing", upload)).toBe(false);
    const saved = entries.values().next().value;
    if (!saved) {
      throw new Error("Expected cached response");
    }
    const expired = new Response(await saved.blob(), {
      headers: saved.headers,
    });
    expired.headers.set("Date", new Date(0).toUTCString());
    entries.set(`${origin}/share-target/${id}`, expired);
    expect(await uploadSharedFiles(id, upload)).toBe(false);
    expect(upload).not.toHaveBeenCalled();
    expect(entries.size).toBe(0);
  });

  it("only accepts file entries from the configured form field", () => {
    const data = new FormData();
    data.append("files", "not a file");
    data.append("other", new File(["other"], "other.txt"));
    expect(getSharedFiles(data)).toEqual([]);
  });

  it("handles a multipart share locally and redirects to the root file view", async () => {
    const data = new FormData();
    data.append("files", new File(["hello"], "hello.txt"));
    const response = await receiveSharedFiles(
      new Request(`${origin}/share-target`, {
        method: "POST",
        headers: { Origin: origin },
        body: data,
      }),
    );
    const target = new URL(response.headers.get("Location") ?? "");
    expect(response.status).toBe(303);
    expect(target.origin + target.pathname).toBe(`${origin}/files`);
    expect(target.searchParams.get("sharedFiles")).not.toBe("error");
    expect(entries.size).toBe(1);
  });

  it.each(["https://other.example", "null"])(
    "rejects a forged share with origin %s without storing its files",
    async (sourceOrigin) => {
      const data = new FormData();
      data.append("files", new File(["forged"], "forged.txt"));
      const response = await receiveSharedFiles(
        new Request(`${origin}/share-target`, {
          method: "POST",
          headers: { Origin: sourceOrigin },
          body: data,
        }),
      );
      expect(response.status).toBe(403);
      expect(entries.size).toBe(0);
    },
  );

  it("returns an explicit receive error for empty shares and storage failure", async () => {
    const response = await receiveSharedFiles(
      new Request(`${origin}/share-target`, {
        method: "POST",
        body: new FormData(),
      }),
    );
    expect(response.headers.get("Location")).toBe(
      `${origin}/files?sharedFiles=error`,
    );
    vi.stubGlobal("caches", {
      open: async () => {
        throw new Error("quota exceeded");
      },
    });
    const data = new FormData();
    data.append("files", new File(["hello"], "hello.txt"));
    const failure = await receiveSharedFiles(
      new Request(`${origin}/share-target`, { method: "POST", body: data }),
    );
    expect(failure.headers.get("Location")).toBe(
      `${origin}/files?sharedFiles=error`,
    );
  });
});
