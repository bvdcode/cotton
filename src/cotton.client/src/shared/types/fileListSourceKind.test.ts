import { describe, expect, it } from "vitest";
import { getFileListCapabilities } from "./fileListSourceKind";

describe("file list source capabilities", () => {
  it("allows full editing in regular folders", () => {
    expect(getFileListCapabilities("nodes")).toEqual({
      canUpload: true,
      canDelete: true,
      canRestore: false,
      canRename: true,
      canCutPaste: true,
      isReadOnly: false,
    });
  });

  it("limits search results to navigation and file actions", () => {
    expect(getFileListCapabilities("search")).toEqual({
      canUpload: false,
      canDelete: false,
      canRestore: false,
      canRename: false,
      canCutPaste: false,
      isReadOnly: false,
    });
  });

  it("marks shared lists as read-only", () => {
    expect(getFileListCapabilities("share")).toEqual({
      canUpload: false,
      canDelete: false,
      canRestore: false,
      canRename: false,
      canCutPaste: false,
      isReadOnly: true,
    });
  });
});
