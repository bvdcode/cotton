import { describe, expect, it } from "vitest";
import {
  __testing__,
  decodeEnvelopePreference,
  encodeEnvelopePreference,
  rewrapForNewPassword,
  setupEnvelope,
  unlockWithPassword,
  unlockWithRecovery,
} from "./envelope";
import {
  CorruptedContainerError,
  InvalidCryptoInputError,
  UnsupportedVersionError,
  WrongUnlockError,
} from "./errors";
import type { Argon2idParams } from "./keys";

const testKdf = {
  memoryKiB: 8,
  iterations: 1,
  parallelism: 1,
} satisfies Argon2idParams;

async function exportRawKey(key: CryptoKey): Promise<number[]> {
  const raw = await crypto.subtle.exportKey("raw", key);
  return Array.from(new Uint8Array(raw));
}

describe("setupEnvelope / unlockWithPassword", () => {
  it("restores the same master key", async () => {
    const { envelope, masterKey } = await setupEnvelope("correct horse", testKdf);
    const restored = await unlockWithPassword(envelope, "correct horse");

    expect(await exportRawKey(restored)).toEqual(await exportRawKey(masterKey));
  });

  it("rejects the wrong password", async () => {
    const { envelope } = await setupEnvelope("right", testKdf);

    await expect(unlockWithPassword(envelope, "wrong")).rejects.toBeInstanceOf(
      WrongUnlockError,
    );
  });

  it("does not create an envelope with an empty password", async () => {
    await expect(setupEnvelope("", testKdf)).rejects.toBeInstanceOf(
      InvalidCryptoInputError,
    );
  });
});

describe("setupEnvelope / unlockWithRecovery", () => {
  it("restores the same master key from the recovery phrase", async () => {
    const { envelope, masterKey, recoveryPhrase } = await setupEnvelope("pw", testKdf);
    const restored = await unlockWithRecovery(envelope, recoveryPhrase);

    expect(recoveryPhrase.split(" ")).toHaveLength(24);
    expect(await exportRawKey(restored)).toEqual(await exportRawKey(masterKey));
  });

  it("accepts a normalized recovery phrase", async () => {
    const { envelope, masterKey, recoveryPhrase } = await setupEnvelope("pw", testKdf);
    const noisy = ` ${recoveryPhrase.replaceAll(" ", "   ").toUpperCase()} `;
    const restored = await unlockWithRecovery(envelope, noisy);

    expect(await exportRawKey(restored)).toEqual(await exportRawKey(masterKey));
  });

  it("rejects a different valid recovery phrase", async () => {
    const { envelope } = await setupEnvelope("pw", testKdf);
    const otherPhrase = `${"abandon ".repeat(23)}art`.trim();

    await expect(unlockWithRecovery(envelope, otherPhrase)).rejects.toBeInstanceOf(
      WrongUnlockError,
    );
  });
});

describe("rewrapForNewPassword", () => {
  it("moves password unlock to the new password and keeps recovery intact", async () => {
    const { envelope, masterKey, recoveryPhrase } = await setupEnvelope("old", testKdf);
    const updated = await rewrapForNewPassword(envelope, masterKey, "new", testKdf);
    const viaPassword = await unlockWithPassword(updated, "new");
    const viaRecovery = await unlockWithRecovery(updated, recoveryPhrase);

    await expect(unlockWithPassword(updated, "old")).rejects.toBeInstanceOf(
      WrongUnlockError,
    );
    expect(await exportRawKey(viaPassword)).toEqual(await exportRawKey(masterKey));
    expect(await exportRawKey(viaRecovery)).toEqual(await exportRawKey(masterKey));
  });
});

describe("envelope preference encoding", () => {
  it("round-trips through base64 preference storage", async () => {
    const { envelope, masterKey } = await setupEnvelope("pw", testKdf);
    const encoded = encodeEnvelopePreference(envelope);
    const decoded = decodeEnvelopePreference(encoded);
    const restored = await unlockWithPassword(decoded, "pw");

    expect(await exportRawKey(restored)).toEqual(await exportRawKey(masterKey));
  });

  it("rejects invalid base64", () => {
    expect(() => decodeEnvelopePreference("not base64!")).toThrow(CorruptedContainerError);
  });
});

describe("envelope parser", () => {
  it("rejects an unknown version or KDF", async () => {
    const { envelope } = await setupEnvelope("pw", testKdf);
    const unknownVersion = envelope.slice();
    unknownVersion[0] = 99;
    const unknownKdf = envelope.slice();
    unknownKdf[1] = 99;

    expect(() => __testing__.parseEnvelope(unknownVersion)).toThrow(
      UnsupportedVersionError,
    );
    expect(() => __testing__.parseEnvelope(unknownKdf)).toThrow(UnsupportedVersionError);
  });

  it("rejects truncation and trailing bytes", async () => {
    const { envelope } = await setupEnvelope("pw", testKdf);
    const trailing = new Uint8Array(envelope.length + 1);
    trailing.set(envelope);

    expect(() => __testing__.parseEnvelope(envelope.slice(0, 24))).toThrow(
      CorruptedContainerError,
    );
    expect(() => __testing__.parseEnvelope(trailing)).toThrow(CorruptedContainerError);
  });

  it("rejects resource-heavy KDF parameters from stored data", async () => {
    const { envelope } = await setupEnvelope("pw", testKdf);
    const modified = envelope.slice();
    const passwordMemoryOffset = 2 + 16;
    new DataView(modified.buffer).setUint32(passwordMemoryOffset, 999 * 1024, false);

    expect(() => __testing__.parseEnvelope(modified)).toThrow(CorruptedContainerError);
  });
});
