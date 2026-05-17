import { describe, expect, it } from "vitest";
import { ClientEncryptionSizeLimitError } from "./errors";
import {
  assertClientEncryptionBlobPipelineSize,
  CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES,
} from "./limits";

describe("client-side encryption blob pipeline limits", () => {
  it("allows files at the temporary blob pipeline limit", () => {
    expect(() =>
      assertClientEncryptionBlobPipelineSize(
        CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES,
        "encrypt",
      ),
    ).not.toThrow();
  });

  it("rejects files above the temporary blob pipeline limit", () => {
    expect(() =>
      assertClientEncryptionBlobPipelineSize(
        CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES + 1,
        "decrypt",
      ),
    ).toThrow(ClientEncryptionSizeLimitError);
  });
});
