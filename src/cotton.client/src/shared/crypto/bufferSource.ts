/**
 * TypeScript models Uint8Array as potentially SharedArrayBuffer-backed, while
 * WebCrypto accepts only BufferSource. Crypto byte arrays in this module are
 * created with ArrayBuffer-backed constructors, slices, or getRandomValues.
 */
export function asBufferSource(bytes: Uint8Array): BufferSource {
  return bytes as BufferSource;
}
