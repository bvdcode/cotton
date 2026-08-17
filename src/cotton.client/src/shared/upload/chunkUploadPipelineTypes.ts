export interface ChunkSegment {
  start: number;
  end: number;
  networkFailures: number;
}

export interface PreparedChunk {
  segment: ChunkSegment;
  blob: Blob;
  hash: string;
}

export interface UploadedChunkSegment {
  start: number;
  end: number;
  hash: string;
}

export const getChunkLength = (segment: ChunkSegment): number =>
  segment.end - segment.start;
