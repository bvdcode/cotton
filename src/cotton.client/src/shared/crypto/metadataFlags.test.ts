import { describe, expect, it } from "vitest";
import {
  FOLDER_ENCRYPTION_POLICY_KEY,
  getFolderEncryptionPolicyState,
  getFolderEncryptionPolicyStateFromParentResolver,
  getOriginalContentType,
  isFileEncrypted,
  isFolderEncryptionPolicyEnabled,
  type FolderEncryptionPolicyNode,
} from "./metadataFlags";
import { ENCRYPTED_FLAG_KEY, ORIGINAL_CONTENT_TYPE_KEY } from "./fileCipher";

describe("metadataFlags", () => {
  it("detects encrypted files only when the flag is explicitly true", () => {
    expect(isFileEncrypted({ [ENCRYPTED_FLAG_KEY]: "true" })).toBe(true);
    expect(isFileEncrypted({ [ENCRYPTED_FLAG_KEY]: "false" })).toBe(false);
    expect(isFileEncrypted({})).toBe(false);
    expect(isFileEncrypted(null)).toBe(false);
  });

  it("detects folder encryption policy only when the flag is explicitly true", () => {
    expect(
      isFolderEncryptionPolicyEnabled({
        [FOLDER_ENCRYPTION_POLICY_KEY]: "true",
      }),
    ).toBe(true);
    expect(
      isFolderEncryptionPolicyEnabled({
        [FOLDER_ENCRYPTION_POLICY_KEY]: "false",
      }),
    ).toBe(false);
    expect(isFolderEncryptionPolicyEnabled(undefined)).toBe(false);
  });

  it("derives effective folder policy from ancestors", () => {
    const parent = {
      id: "parent",
      metadata: { [FOLDER_ENCRYPTION_POLICY_KEY]: "true" },
    };
    const child = { id: "child", metadata: {} };

    expect(getFolderEncryptionPolicyState(child, [parent])).toEqual({
      explicitEnabled: false,
      inheritedEnabled: true,
      effectiveEnabled: true,
    });
  });

  it("walks parent links when deriving effective folder policy", () => {
    const nodes = new Map<string, FolderEncryptionPolicyNode>([
      [
        "root",
        {
          id: "root",
          parentId: null,
          metadata: { [FOLDER_ENCRYPTION_POLICY_KEY]: "true" },
        },
      ],
      ["parent", { id: "parent", parentId: "root", metadata: {} }],
    ]);
    const child = { id: "child", parentId: "parent", metadata: {} };

    expect(
      getFolderEncryptionPolicyStateFromParentResolver(child, (id) =>
        nodes.get(id),
      ),
    ).toEqual({
      explicitEnabled: false,
      inheritedEnabled: true,
      effectiveEnabled: true,
    });
  });

  it("reads the original content type from file metadata", () => {
    expect(
      getOriginalContentType({ [ORIGINAL_CONTENT_TYPE_KEY]: "image/png" }),
    ).toBe("image/png");
    expect(getOriginalContentType({})).toBeUndefined();
  });
});
