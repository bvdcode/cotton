import { describe, expect, it } from "vitest";
import {
  base64UrlToBuffer,
  bufferToBase64Url,
  toCredentialCreationOptions,
  toCredentialRequestOptions,
} from "./webauthn";

const toBytes = (source: BufferSource): number[] => {
  const view = ArrayBuffer.isView(source)
    ? new Uint8Array(source.buffer, source.byteOffset, source.byteLength)
    : new Uint8Array(source);

  return [...view];
};

describe("passkey WebAuthn helpers", () => {
  it("round-trips base64url encoded ArrayBuffers", () => {
    const source = new Uint8Array([0, 1, 2, 253, 254, 255]).buffer;

    const encoded = bufferToBase64Url(source);
    const decoded = new Uint8Array(base64UrlToBuffer(encoded));

    expect(encoded).toBe("AAEC_f7_");
    expect([...decoded]).toEqual([0, 1, 2, 253, 254, 255]);
  });

  it("converts registration options into browser ArrayBuffers", () => {
    const options = toCredentialCreationOptions({
      rp: { id: "cloud.example", name: "Cotton Cloud" },
      user: {
        id: "AQID",
        name: "alice",
        displayName: "Alice",
      },
      challenge: "BAUG",
      pubKeyCredParams: [{ type: "public-key", alg: -7 }],
      timeout: 60000,
      attestation: "none",
      authenticatorSelection: {
        residentKey: "required",
        userVerification: "required",
      },
      excludeCredentials: [{ type: "public-key", id: "BwgJ" }],
    });

    expect(toBytes(options.challenge)).toEqual([4, 5, 6]);
    expect(toBytes(options.user.id)).toEqual([1, 2, 3]);
    expect(options.excludeCredentials?.[0].id).toBeInstanceOf(ArrayBuffer);
  });

  it("converts assertion options into browser ArrayBuffers", () => {
    const options = toCredentialRequestOptions({
      challenge: "AQID",
      rpId: "cloud.example",
      timeout: 60000,
      userVerification: "required",
      allowCredentials: [{ type: "public-key", id: "BAUG" }],
    });

    expect(toBytes(options.challenge)).toEqual([1, 2, 3]);
    expect(options.allowCredentials?.[0].id).toBeInstanceOf(ArrayBuffer);
  });
});
