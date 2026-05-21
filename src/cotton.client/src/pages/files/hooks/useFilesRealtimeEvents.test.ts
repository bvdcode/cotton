import { describe, expect, it } from "vitest";
import { shouldInvalidateCurrentNode } from "./useFilesRealtimeEvents";
import { HUB_METHODS } from "../../../shared/signalr";

const currentNodeId = "11111111-1111-4111-8111-111111111111";
const deletedFileId = "22222222-2222-4222-8222-222222222222";
const otherNodeId = "33333333-3333-4333-8333-333333333333";

describe("shouldInvalidateCurrentNode", () => {
  it("ignores bare GUID payloads without structured node context", () => {
    expect(
      shouldInvalidateCurrentNode(
        HUB_METHODS.FileDeleted,
        [deletedFileId],
        currentNodeId,
      ),
    ).toBe(false);
  });

  it("invalidates when a created child folder belongs to the current node", () => {
    expect(
      shouldInvalidateCurrentNode(
        HUB_METHODS.NodeCreated,
        [{ id: deletedFileId, parentId: currentNodeId }],
        currentNodeId,
      ),
    ).toBe(true);
  });

  it("invalidates when an externally-created file belongs to the current node", () => {
    expect(
      shouldInvalidateCurrentNode(
        HUB_METHODS.FileCreated,
        [{ id: deletedFileId, nodeId: currentNodeId, name: "plain.txt" }],
        currentNodeId,
      ),
    ).toBe(true);
  });

  it("accepts version-agnostic .NET Guid node ids", () => {
    const uuidV7NodeId = "019e5176-5482-7e90-04b9-fded5783ada4";

    expect(
      shouldInvalidateCurrentNode(
        HUB_METHODS.FileCreated,
        [{ id: deletedFileId, nodeId: uuidV7NodeId, name: "audio.ogg" }],
        uuidV7NodeId,
      ),
    ).toBe(true);
  });

  it("invalidates when an externally-updated file belongs to the current node", () => {
    expect(
      shouldInvalidateCurrentNode(
        HUB_METHODS.FileUpdated,
        [
          {
            id: deletedFileId,
            nodeId: currentNodeId,
            metadata: { isClientEncrypted: "true" },
          },
        ],
        currentNodeId,
      ),
    ).toBe(true);
  });

  it("invalidates when a renamed child folder belongs to the current node", () => {
    expect(
      shouldInvalidateCurrentNode(
        HUB_METHODS.NodeRenamed,
        [{ id: deletedFileId, parentId: currentNodeId, name: "Renamed" }],
        currentNodeId,
      ),
    ).toBe(true);
  });

  it("invalidates when structured delete payload includes the parent node", () => {
    expect(
      shouldInvalidateCurrentNode(
        HUB_METHODS.FileDeleted,
        [{ nodeFileId: deletedFileId, parentNodeId: currentNodeId }],
        currentNodeId,
      ),
    ).toBe(true);
  });

  it("invalidates when a deleted child folder reports the current parent", () => {
    expect(
      shouldInvalidateCurrentNode(
        HUB_METHODS.NodeDeleted,
        [{ nodeId: deletedFileId, parentNodeId: currentNodeId }],
        currentNodeId,
      ),
    ).toBe(true);
  });

  it("skips structured payloads for unrelated parent nodes", () => {
    expect(
      shouldInvalidateCurrentNode(
        HUB_METHODS.FileDeleted,
        [{ nodeFileId: deletedFileId, parentNodeId: otherNodeId }],
        currentNodeId,
      ),
    ).toBe(false);
  });

  it("invalidates when a structured payload refers to the current node itself", () => {
    expect(
      shouldInvalidateCurrentNode(
        HUB_METHODS.NodeRenamed,
        [{ id: currentNodeId, parentId: otherNodeId, name: "Current" }],
        currentNodeId,
      ),
    ).toBe(true);
  });

  it("invalidates both source and target folders for a moved folder", () => {
    expect(
      shouldInvalidateCurrentNode(
        HUB_METHODS.NodeMoved,
        [{ node: { id: deletedFileId }, oldParentId: currentNodeId, newParentId: otherNodeId }],
        currentNodeId,
      ),
    ).toBe(true);

    expect(
      shouldInvalidateCurrentNode(
        HUB_METHODS.NodeMoved,
        [{ node: { id: deletedFileId }, oldParentId: otherNodeId, newParentId: currentNodeId }],
        currentNodeId,
      ),
    ).toBe(true);
  });

  it("does not treat file ids as affected folder ids", () => {
    expect(
      shouldInvalidateCurrentNode(
        HUB_METHODS.FileDeleted,
        [{ nodeFileId: currentNodeId, parentNodeId: otherNodeId }],
        currentNodeId,
      ),
    ).toBe(false);
  });
});
