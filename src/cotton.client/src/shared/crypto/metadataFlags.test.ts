import { describe, expect, it } from "vitest";
import {
  FOLDER_ENCRYPTION_POLICY_KEY,
  getOriginalContentType,
  isFileEncrypted,
  isFolderEncryptionPolicyEnabled,
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

  it("reads the original content type from file metadata", () => {
    expect(
      getOriginalContentType({ [ORIGINAL_CONTENT_TYPE_KEY]: "image/png" }),
    ).toBe("image/png");
    expect(getOriginalContentType({})).toBeUndefined();
  });
});
