import type { HeicImage } from "./HeicImage";

export type HeicDecodeResponse =
  | { type: "decoded"; requestId: number; image: HeicImage }
  | { type: "error"; requestId: number; message: string };
