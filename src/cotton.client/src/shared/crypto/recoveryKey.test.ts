import { describe, expect, it } from "vitest";
import { InvalidRecoveryPhraseError } from "./errors";
import {
  generateRecoveryPhrase,
  normalizeAndValidateRecoveryPhrase,
  recoveryPhraseToEntropy,
  recoveryPhraseToKdfSecret,
  validateRecoveryPhrase,
} from "./recoveryKey";

describe("generateRecoveryPhrase", () => {
  it("produces a 24-word BIP39 phrase", () => {
    const phrase = generateRecoveryPhrase();

    expect(phrase.split(" ")).toHaveLength(24);
    expect(validateRecoveryPhrase(phrase)).toBe(true);
  });

  it("uses fresh entropy for each phrase", () => {
    expect(generateRecoveryPhrase()).not.toBe(generateRecoveryPhrase());
  });
});

describe("normalizeAndValidateRecoveryPhrase", () => {
  it("accepts extra spacing and casing", () => {
    const phrase = generateRecoveryPhrase();
    const noisy = `  ${phrase.replaceAll(" ", "   ").toUpperCase()}  \n`;

    expect(normalizeAndValidateRecoveryPhrase(noisy)).toBe(phrase);
  });

  it("rejects a phrase with the wrong word count", () => {
    expect(() => normalizeAndValidateRecoveryPhrase("abandon abandon")).toThrow(
      InvalidRecoveryPhraseError,
    );
  });

  it("rejects words outside the BIP39 wordlist", () => {
    const phrase = new Array(24).fill("notarealword").join(" ");

    expect(() => normalizeAndValidateRecoveryPhrase(phrase)).toThrow(
      InvalidRecoveryPhraseError,
    );
  });

  it("rejects a phrase with a bad checksum", () => {
    const words = generateRecoveryPhrase().split(" ");
    words[words.length - 1] =
      words[words.length - 1] === "abandon" ? "ability" : "abandon";

    expect(() => normalizeAndValidateRecoveryPhrase(words.join(" "))).toThrow(
      InvalidRecoveryPhraseError,
    );
  });
});

describe("recovery phrase entropy", () => {
  it("converts a valid phrase into 32 bytes and a stable KDF secret", () => {
    const phrase = `${"abandon ".repeat(23)}art`.trim();
    const entropy = recoveryPhraseToEntropy(phrase);

    expect(entropy).toHaveLength(32);
    expect(recoveryPhraseToKdfSecret(phrase)).toMatch(/^[0-9a-f]{64}$/);
    expect(recoveryPhraseToKdfSecret(phrase.toUpperCase())).toBe(
      recoveryPhraseToKdfSecret(phrase),
    );
  });
});
