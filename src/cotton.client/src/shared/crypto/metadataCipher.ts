import { asBufferSource } from "./bufferSource";
import { base64ToBytes, bytesToBase64 } from "./base64";
import { InvalidCryptoInputError } from "./errors";
import { randomBytes } from "./keys";
import { requireMetadataKey } from "./vault";

const NONCE_BYTES = 12;

export async function encryptMetadata(value: string): Promise<string> {
  const key = await requireMetadataKey();
  const iv = randomBytes(NONCE_BYTES);
  const ciphertext = new Uint8Array(
    await crypto.subtle.encrypt(
      { name: "AES-GCM", iv: asBufferSource(iv) },
      key,
      asBufferSource(new TextEncoder().encode(value)),
    ),
  );
  const payload = new Uint8Array(iv.length + ciphertext.length);
  payload.set(iv);
  payload.set(ciphertext, iv.length);
  return bytesToBase64(payload);
}

export async function decryptMetadata(value: string): Promise<string> {
  const key = await requireMetadataKey();
  const payload = base64ToBytes(value);
  if (payload.length <= NONCE_BYTES) {
    throw new InvalidCryptoInputError("Display metadata payload is too short.");
  }
  const plaintext = await crypto.subtle.decrypt(
    { name: "AES-GCM", iv: asBufferSource(payload.subarray(0, NONCE_BYTES)) },
    key,
    asBufferSource(payload.subarray(NONCE_BYTES)),
  );
  return new TextDecoder().decode(plaintext);
}
