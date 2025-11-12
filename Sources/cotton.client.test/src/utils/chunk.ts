export function* chunkBlob(blob: Blob, chunkSize: number): Generator<Blob> {
  if (chunkSize <= 0) throw new Error("chunkSize must be > 0");
  const size = blob.size;
  let offset = 0;
  while (offset < size) {
    const end = Math.min(offset + chunkSize, size);
    yield blob.slice(offset, end);
    offset = end;
  }
}
