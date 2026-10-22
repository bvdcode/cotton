import { afterEach, describe, expect, it, vi } from "vitest";
import { nodesApi, type NodeResponse } from "../api/nodesApi";
import { createFile, createFolder } from "../../test/fileFixtures";
import { findFolderEncryptionMatch } from "./folderEncryptionPresence";

const page = (
  id: string,
  files: NodeResponse["content"]["files"] = [],
  nodes: NodeResponse["content"]["nodes"] = [],
): NodeResponse => ({
  content: { ...createFolder({ id }), files, nodes },
  totalCount: files.length + nodes.length,
});
afterEach(() => vi.restoreAllMocks());

describe("folder encryption presence", () => {
  it("finds a nested file without reading content or converting folder names", async () => {
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockImplementation(async (id) => {
        if (id === "root") {
          return page(
            id,
            [],
            [createFolder({ id: "child" }), createFolder({ id: "unneeded" })],
          );
        }
        return page(id, [
          createFile({ metadata: { isClientEncrypted: "true" } }),
        ]);
      });
    const signal = new AbortController().signal;
    expect(
      await findFolderEncryptionMatch("root", "encrypted-files", signal),
    ).toBe(true);
    expect(get.mock.calls.map(([id]) => id)).toEqual(["root", "child"]);
    expect(
      get.mock.calls.every(([, options]) => options?.signal === signal),
    ).toBe(true);
  });

  it("reuses the first page and keeps its page size when following pages", async () => {
    const first = {
      ...page("root", [
        createFile({ metadata: { isClientEncrypted: "true" } }),
      ]),
      totalCount: 2,
    };
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockResolvedValue(page("root", [createFile()]));
    const signal = new AbortController().signal;
    expect(
      await findFolderEncryptionMatch("root", "plain-files", signal, {
        response: first,
        pageSize: 1,
      }),
    ).toBe(true);
    expect(get).toHaveBeenCalledExactlyOnceWith("root", {
      page: 2,
      pageSize: 1,
      signal,
    });
  });

  it("does not restrict presence checks to the 250-folder processing batch", async () => {
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockImplementation(async (id) => {
        const index = Number(id);
        return index === 260
          ? page(id, [createFile()])
          : page(id, [], [createFolder({ id: String(index + 1) })]);
      });
    expect(
      await findFolderEncryptionMatch(
        "0",
        "plain-files",
        new AbortController().signal,
      ),
    ).toBe(true);
    expect(get).toHaveBeenCalledTimes(261);
  });

  it("finishes an empty cyclic tree and does not count folder policy as encrypted content", async () => {
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockResolvedValue(
        page(
          "root",
          [],
          [
            createFolder({
              id: "root",
              metadata: { isClientEncryptionEnabled: "true" },
            }),
          ],
        ),
      );
    expect(
      await findFolderEncryptionMatch(
        "root",
        "encrypted-files",
        new AbortController().signal,
      ),
    ).toBe(false);
    expect(get).toHaveBeenCalledOnce();
  });

  it("checks direct encrypted names without walking child folders", async () => {
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockResolvedValue(page("root", [], [createFolder({ id: "child" })]));
    expect(
      await findFolderEncryptionMatch(
        "root",
        "encrypted-names",
        new AbortController().signal,
      ),
    ).toBe(false);
    expect(get).toHaveBeenCalledOnce();
    get.mockResolvedValue(
      page("root", [], [createFolder({ metadata: { en: "opaque" } })]),
    );
    expect(
      await findFolderEncryptionMatch(
        "root",
        "encrypted-names",
        new AbortController().signal,
      ),
    ).toBe(true);
  });

  it("stops after cancellation even if an in-flight response arrives", async () => {
    const controller = new AbortController();
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockImplementation(async () => {
        controller.abort();
        return page("root", [], [createFolder({ id: "child" })]);
      });
    await expect(
      findFolderEncryptionMatch("root", "plain-files", controller.signal),
    ).rejects.toMatchObject({ name: "AbortError" });
    expect(get).toHaveBeenCalledOnce();
  });

  it("propagates a failed page instead of reporting that no files match", async () => {
    vi.spyOn(nodesApi, "getChildren").mockRejectedValue(
      new Error("Unavailable"),
    );
    await expect(
      findFolderEncryptionMatch(
        "root",
        "plain-files",
        new AbortController().signal,
      ),
    ).rejects.toThrow("Unavailable");
  });
});
