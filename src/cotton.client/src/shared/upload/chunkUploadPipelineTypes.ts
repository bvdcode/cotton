export interface ChunkSegment {
  start: number;
  end: number;
  networkFailures: number;
}

export interface PreparedChunk {
  segment: ChunkSegment;
  buffer: ArrayBuffer;
  hash: string;
  contentType: string;
}

export interface UploadedChunkSegment {
  start: number;
  end: number;
  hash: string;
}

export const getChunkLength = (segment: ChunkSegment): number =>
  segment.end - segment.start;
