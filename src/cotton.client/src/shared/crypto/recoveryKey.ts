import {
  generateMnemonic,
  mnemonicToEntropy,
  validateMnemonic,
} from "@scure/bip39";
import { wordlist } from "@scure/bip39/wordlists/english";
import { InvalidRecoveryPhraseError } from "./errors";

export const RECOVERY_ENTROPY_BITS = 256;
export const RECOVERY_ENTROPY_BYTES = RECOVERY_ENTROPY_BITS / 8;
export const RECOVERY_WORD_COUNT = 24;

export function generateRecoveryPhrase(): string {
  return generateMnemonic(wordlist, RECOVERY_ENTROPY_BITS);
}

export function normalizeRecoveryPhrase(input: string): string {
  return input.trim().toLowerCase().split(/\s+/).join(" ");
}

export function validateRecoveryPhrase(input: string): boolean {
  const normalized = normalizeRecoveryPhrase(input);
  return (
    normalized.split(" ").length === RECOVERY_WORD_COUNT &&
    validateMnemonic(normalized, wordlist)
  );
}

export function normalizeAndValidateRecoveryPhrase(input: string): string {
  const normalized = normalizeRecoveryPhrase(input);

  if (normalized.split(" ").length !== RECOVERY_WORD_COUNT) {
    throw new InvalidRecoveryPhraseError(
      `Recovery phrase must be exactly ${RECOVERY_WORD_COUNT} words.`,
    );
  }

  if (!validateMnemonic(normalized, wordlist)) {
    throw new InvalidRecoveryPhraseError("Recovery phrase failed BIP39 validation.");
  }

  return normalized;
}

export function recoveryPhraseToEntropy(phrase: string): Uint8Array {
  const normalized = normalizeAndValidateRecoveryPhrase(phrase);
  const entropy = mnemonicToEntropy(normalized, wordlist);

  if (entropy.length !== RECOVERY_ENTROPY_BYTES) {
    throw new InvalidRecoveryPhraseError("Recovery phrase entropy length is unsupported.");
  }

  return entropy;
}

export function recoveryPhraseToKdfSecret(phrase: string): string {
  return bytesToHex(recoveryPhraseToEntropy(phrase));
}

function bytesToHex(bytes: Uint8Array): string {
  let output = "";

  for (let i = 0; i < bytes.length; i += 1) {
    output += bytes[i].toString(16).padStart(2, "0");
  }

  return output;
}
