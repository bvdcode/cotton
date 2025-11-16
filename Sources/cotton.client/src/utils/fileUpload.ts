export const DEFAULT_CHUNK_SIZE = 4 * 1024 * 1024; // 4MB
export const DEFAULT_CONCURRENCY = 2;
export const MIN_CHUNK_SIZE = 1 * 1024 * 1024; // 1MB

export interface AdaptiveChunkUploader {
  currentChunkSize: number;
  adjustChunkSize(uploadTimeMs: number): void;
}

export function createAdaptiveUploader(
  initialSize: number,
  maxSize: number,
): AdaptiveChunkUploader {
  let currentChunkSize = Math.max(
    MIN_CHUNK_SIZE,
    Math.min(initialSize, maxSize),
  );

  return {
    get currentChunkSize() {
      return currentChunkSize;
    },
    adjustChunkSize(uploadTimeMs: number) {
      const timeSec = uploadTimeMs / 1000;

      if (timeSec < 2) {
        // Fast upload: double chunk size
        const newSize = Math.min(currentChunkSize * 2, maxSize);
        if (newSize !== currentChunkSize) {
          currentChunkSize = newSize;
          console.log(
            `Chunk size increased to ${formatBytes(currentChunkSize)} (upload took ${timeSec.toFixed(2)}s)`,
          );
        }
      } else if (timeSec > 10) {
        // Slow upload: halve chunk size
        const newSize = Math.max(currentChunkSize / 2, MIN_CHUNK_SIZE);
        if (newSize !== currentChunkSize) {
          currentChunkSize = newSize;
          console.log(
            `Chunk size decreased to ${formatBytes(currentChunkSize)} (upload took ${timeSec.toFixed(2)}s)`,
          );
        }
      }
      // 2-10 seconds: keep current size
    },
  };
}

export function* chunkBlob(blob: Blob, chunkSize: number): Generator<Blob> {
  let offset = 0;
  while (offset < blob.size) {
    const end = Math.min(offset + chunkSize, blob.size);
    yield blob.slice(offset, end);
    offset = end;
  }
}

export function readBlobArrayBuffer(blob: Blob): Promise<ArrayBuffer> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as ArrayBuffer);
    reader.onerror = () => reject(reader.error);
    reader.readAsArrayBuffer(blob);
  });
}

export async function sha256(buffer: ArrayBuffer): Promise<string> {
  const hashBuffer = await crypto.subtle.digest("SHA-256", buffer);
  const hashArray = Array.from(new Uint8Array(hashBuffer));
  return hashArray.map((b) => b.toString(16).padStart(2, "0")).join("");
}

export async function hashBlob(blob: Blob): Promise<string> {
  const ab = await readBlobArrayBuffer(blob);
  return sha256(ab);
}

export async function hashFile(file: File): Promise<string> {
  // Note: loads entire file into memory; good enough initially. For very large files, switch to streaming.
  const ab = await readBlobArrayBuffer(file);
  return sha256(ab);
}

export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes)) return "0 B";
  const units = ["B", "KB", "MB", "GB", "TB"]; 
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  const val = bytes / Math.pow(1024, Math.min(i, units.length - 1));
  return `${val.toFixed(1)} ${units[Math.min(i, units.length - 1)]}`;
}

export function formatBytesPerSecond(bps: number): string {
  return `${formatBytes(bps)}/s`;
}
