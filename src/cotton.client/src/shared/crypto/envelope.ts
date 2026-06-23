import { base64ToBytes, bytesToBase64 } from "./base64";
import {
  DEFAULT_ARGON2ID,
  KDF_SALT_BYTES,
  deriveKek,
  generateMasterKey,
  isValidArgon2idParams,
  randomBytes,
  unwrapMasterKey,
  wrapMasterKey,
} from "./keys";
import type { Argon2idParams } from "./keys";
import {
  generateRecoveryPhrase,
  recoveryPhraseToKdfSecret,
} from "./recoveryKey";
import {
  CorruptedContainerError,
  InvalidCryptoInputError,
  UnsupportedVersionError,
} from "./errors";

export const ENVELOPE_PREFERENCE_KEY = "cryptoEnvelope";

const ENVELOPE_VERSION = 1;
const KDF_ARGON2ID = 1;
const WRAP_SECTION_FIXED_BYTES = KDF_SALT_BYTES + 4 + 4 + 1 + 2;

interface EnvelopeWrapSection {
  salt: Uint8Array;
  kdfParams: Argon2idParams;
  wrappedMasterKey: Uint8Array;
}

interface EnvelopeData {
  password: EnvelopeWrapSection;
  recovery: EnvelopeWrapSection;
}

export interface SetupEnvelopeResult {
  envelope: Uint8Array;
  masterKey: CryptoKey;
  recoveryPhrase: string;
}

export function encodeEnvelopePreference(envelope: Uint8Array): string {
  return bytesToBase64(envelope);
}

export function decodeEnvelopePreference(value: string): Uint8Array {
  try {
    return base64ToBytes(value);
  } catch {
    throw new CorruptedContainerError(
      "Envelope preference is not valid base64.",
    );
  }
}

export async function setupEnvelope(
  password: string,
  params: Argon2idParams = DEFAULT_ARGON2ID,
): Promise<SetupEnvelopeResult> {
  assertNonEmptyPassword(password);

  const masterKey = await generateMasterKey();
  const recoveryPhrase = generateRecoveryPhrase();
  const passwordSalt = randomBytes(KDF_SALT_BYTES);
  const recoverySalt = randomBytes(KDF_SALT_BYTES);
  const [passwordKek, recoveryKek] = await Promise.all([
    deriveKek(password, passwordSalt, params),
    deriveKek(recoveryPhraseToKdfSecret(recoveryPhrase), recoverySalt, params),
  ]);
  const [passwordWrap, recoveryWrap] = await Promise.all([
    wrapMasterKey(passwordKek, masterKey),
    wrapMasterKey(recoveryKek, masterKey),
  ]);

  return {
    envelope: serializeEnvelope({
      password: {
        salt: passwordSalt,
        kdfParams: params,
        wrappedMasterKey: passwordWrap,
      },
      recovery: {
        salt: recoverySalt,
        kdfParams: params,
        wrappedMasterKey: recoveryWrap,
      },
    }),
    masterKey,
    recoveryPhrase,
  };
}

export async function unlockWithPassword(
  envelopeBlob: Uint8Array,
  password: string,
): Promise<CryptoKey> {
  const envelope = parseEnvelope(envelopeBlob);
  const kek = await deriveKek(
    password,
    envelope.password.salt,
    envelope.password.kdfParams,
  );
  return unwrapMasterKey(kek, envelope.password.wrappedMasterKey);
}

export async function unlockWithRecovery(
  envelopeBlob: Uint8Array,
  recoveryPhrase: string,
): Promise<CryptoKey> {
  const envelope = parseEnvelope(envelopeBlob);
  const kek = await deriveKek(
    recoveryPhraseToKdfSecret(recoveryPhrase),
    envelope.recovery.salt,
    envelope.recovery.kdfParams,
  );
  return unwrapMasterKey(kek, envelope.recovery.wrappedMasterKey);
}

export async function rewrapForNewPassword(
  envelopeBlob: Uint8Array,
  masterKey: CryptoKey,
  newPassword: string,
  params: Argon2idParams = DEFAULT_ARGON2ID,
): Promise<Uint8Array> {
  assertNonEmptyPassword(newPassword);

  const envelope = parseEnvelope(envelopeBlob);
  const passwordSalt = randomBytes(KDF_SALT_BYTES);
  const passwordKek = await deriveKek(newPassword, passwordSalt, params);
  const passwordWrap = await wrapMasterKey(passwordKek, masterKey);

  return serializeEnvelope({
    password: {
      salt: passwordSalt,
      kdfParams: params,
      wrappedMasterKey: passwordWrap,
    },
    recovery: envelope.recovery,
  });
}

function serializeEnvelope(envelope: EnvelopeData): Uint8Array {
  assertWrapSection(envelope.password);
  assertWrapSection(envelope.recovery);

  const totalLength =
    2 +
    WRAP_SECTION_FIXED_BYTES +
    envelope.password.wrappedMasterKey.length +
    WRAP_SECTION_FIXED_BYTES +
    envelope.recovery.wrappedMasterKey.length;
  const output = new Uint8Array(totalLength);
  const view = new DataView(output.buffer);
  let offset = 0;

  output[offset] = ENVELOPE_VERSION;
  offset += 1;
  output[offset] = KDF_ARGON2ID;
  offset += 1;
  offset = writeWrapSection(output, view, offset, envelope.password);
  writeWrapSection(output, view, offset, envelope.recovery);

  return output;
}

function parseEnvelope(blob: Uint8Array): EnvelopeData {
  if (blob.length < 2) {
    throw new CorruptedContainerError("Envelope is truncated.");
  }

  const view = new DataView(blob.buffer, blob.byteOffset, blob.byteLength);
  let offset = 0;
  const version = blob[offset];
  offset += 1;
  const kdfAlgorithm = blob[offset];
  offset += 1;

  if (version !== ENVELOPE_VERSION) {
    throw new UnsupportedVersionError(
      `Unsupported envelope version: ${version}.`,
    );
  }

  if (kdfAlgorithm !== KDF_ARGON2ID) {
    throw new UnsupportedVersionError(
      `Unsupported envelope KDF: ${kdfAlgorithm}.`,
    );
  }

  const password = readWrapSection(
    blob,
    view,
    () => offset,
    (next) => {
      offset = next;
    },
  );
  const recovery = readWrapSection(
    blob,
    view,
    () => offset,
    (next) => {
      offset = next;
    },
  );

  if (offset !== blob.length) {
    throw new CorruptedContainerError("Envelope has trailing bytes.");
  }

  return { password, recovery };
}

function writeWrapSection(
  output: Uint8Array,
  view: DataView,
  offset: number,
  section: EnvelopeWrapSection,
): number {
  output.set(section.salt, offset);
  offset += KDF_SALT_BYTES;
  view.setUint32(offset, section.kdfParams.memoryKiB, false);
  offset += 4;
  view.setUint32(offset, section.kdfParams.iterations, false);
  offset += 4;
  output[offset] = section.kdfParams.parallelism;
  offset += 1;
  view.setUint16(offset, section.wrappedMasterKey.length, false);
  offset += 2;
  output.set(section.wrappedMasterKey, offset);
  return offset + section.wrappedMasterKey.length;
}

function readWrapSection(
  blob: Uint8Array,
  view: DataView,
  getOffset: () => number,
  setOffset: (offset: number) => void,
): EnvelopeWrapSection {
  let offset = getOffset();

  if (offset + WRAP_SECTION_FIXED_BYTES > blob.length) {
    throw new CorruptedContainerError(
      "Envelope is truncated inside wrap section.",
    );
  }

  const salt = blob.slice(offset, offset + KDF_SALT_BYTES);
  offset += KDF_SALT_BYTES;
  const memoryKiB = view.getUint32(offset, false);
  offset += 4;
  const iterations = view.getUint32(offset, false);
  offset += 4;
  const parallelism = blob[offset];
  offset += 1;
  const kdfParams = { memoryKiB, iterations, parallelism };

  if (!isValidArgon2idParams(kdfParams)) {
    throw new CorruptedContainerError(
      "Envelope contains invalid KDF parameters.",
    );
  }

  const wrappedLength = view.getUint16(offset, false);
  offset += 2;

  if (wrappedLength <= 0 || offset + wrappedLength > blob.length) {
    throw new CorruptedContainerError(
      "Envelope is truncated inside wrapped key.",
    );
  }

  const wrappedMasterKey = blob.slice(offset, offset + wrappedLength);
  offset += wrappedLength;
  setOffset(offset);

  return { salt, kdfParams, wrappedMasterKey };
}

function assertWrapSection(section: EnvelopeWrapSection): void {
  if (section.salt.length !== KDF_SALT_BYTES) {
    throw new InvalidCryptoInputError(
      `Envelope salt must be ${KDF_SALT_BYTES} bytes.`,
    );
  }

  if (!isValidArgon2idParams(section.kdfParams)) {
    throw new InvalidCryptoInputError("Envelope KDF parameters are invalid.");
  }

  if (
    section.wrappedMasterKey.length <= 0 ||
    section.wrappedMasterKey.length > 0xffff
  ) {
    throw new InvalidCryptoInputError("Wrapped master key length is invalid.");
  }
}

function assertNonEmptyPassword(password: string): void {
  if (password.length === 0) {
    throw new InvalidCryptoInputError("Password must not be empty.");
  }
}

export const __testing__ = {
  parseEnvelope,
  serializeEnvelope,
};
