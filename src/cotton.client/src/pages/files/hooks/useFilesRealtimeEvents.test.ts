import { describe, expect, it } from "vitest";
import { shouldInvalidateCurrentNode } from "./useFilesRealtimeEvents";

const currentNodeId = "11111111-1111-4111-8111-111111111111";
const deletedFileId = "22222222-2222-4222-8222-222222222222";
const otherNodeId = "33333333-3333-4333-8333-333333333333";

describe("shouldInvalidateCurrentNode", () => {
  it("treats bare GUID events as legacy payloads that require invalidation", () => {
    expect(shouldInvalidateCurrentNode([deletedFileId], currentNodeId)).toBe(true);
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
});
