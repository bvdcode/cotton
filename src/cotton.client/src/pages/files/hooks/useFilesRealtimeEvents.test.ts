import { describe, expect, it } from "vitest";
import { shouldInvalidateCurrentNode } from "./useFilesRealtimeEvents";

const currentNodeId = "11111111-1111-4111-8111-111111111111";
const deletedFileId = "22222222-2222-4222-8222-222222222222";
const otherNodeId = "33333333-3333-4333-8333-333333333333";

describe("shouldInvalidateCurrentNode", () => {
  it("ignores bare GUID payloads without structured node context", () => {
    expect(shouldInvalidateCurrentNode([deletedFileId], currentNodeId)).toBe(
      false,
    );
  });

  it("invalidates when a created child folder belongs to the current node", () => {
    expect(
      shouldInvalidateCurrentNode(
        [{ id: deletedFileId, parentId: currentNodeId }],
        currentNodeId,
      ),
    ).toBe(true);
  });

  it("invalidates when a renamed child folder belongs to the current node", () => {
    expect(
      shouldInvalidateCurrentNode(
        [{ id: deletedFileId, parentId: currentNodeId, name: "Renamed" }],
        currentNodeId,
      ),
    ).toBe(true);
  });

  it("invalidates when structured delete payload includes the parent node", () => {
    expect(
      shouldInvalidateCurrentNode(
        [{ nodeFileId: deletedFileId, parentNodeId: currentNodeId }],
        currentNodeId,
      ),
    ).toBe(true);
  });

  it("skips structured payloads for unrelated parent nodes", () => {
    expect(
      shouldInvalidateCurrentNode(
        [{ nodeFileId: deletedFileId, parentNodeId: otherNodeId }],
        currentNodeId,
      ),
    ).toBe(false);
  });

  it("invalidates when a structured payload refers to the current node itself", () => {
    expect(
      shouldInvalidateCurrentNode(
        [{ id: currentNodeId, parentId: otherNodeId, name: "Current" }],
        currentNodeId,
      ),
    ).toBe(true);
  });
});
