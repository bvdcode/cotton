import { afterEach, describe, expect, it, vi } from "vitest";
import { HeicDecoderClient } from "./HeicDecoderClient";
import { convertHeicBlobToJpeg } from "./convertHeicBlobToJpeg";

describe("convertHeicBlobToJpeg", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("encodes decoded RGBA pixels at the existing JPEG quality and releases the canvas", async () => {
    const source = new Blob(["HEIC"]);
    const pixels = new Uint8Array([10, 20, 30, 255]).buffer;
    const image = { width: 1, height: 1, pixels };
    vi.spyOn(HeicDecoderClient.prototype, "decode").mockResolvedValue(image);
    const converted = new Blob(["JPEG"], { type: "image/jpeg" });
    const canvas = document.createElement("canvas");
    const putImageData = vi.fn();
    const context = Object.assign({}, { putImageData });
    vi.spyOn(document, "createElement").mockReturnValue(canvas);
    Object.defineProperty(canvas, "getContext", {
      value: () => context,
      configurable: true,
    });
    vi.stubGlobal(
      "ImageData",
      class {
        data: Uint8ClampedArray;
        width: number;
        height: number;
        constructor(data: Uint8ClampedArray, width: number, height: number) {
          this.data = data;
          this.width = width;
          this.height = height;
        }
      },
    );
    const encode = vi
      .spyOn(canvas, "toBlob")
      .mockImplementation((callback) => callback(converted));
    await expect(convertHeicBlobToJpeg(source)).resolves.toBe(converted);
    expect(putImageData).toHaveBeenCalledWith(
      expect.objectContaining({
        data: new Uint8ClampedArray(pixels),
        width: 1,
        height: 1,
      }),
      0,
      0,
    );
    expect(encode).toHaveBeenCalledWith(
      expect.any(Function),
      "image/jpeg",
      0.92,
    );
    expect(canvas.width).toBe(0);
    expect(canvas.height).toBe(0);
  });

  it("propagates an invalid HEIC error before creating a canvas", async () => {
    vi.spyOn(HeicDecoderClient.prototype, "decode").mockRejectedValue(
      new Error("Invalid HEIC"),
    );
    const createCanvas = vi.spyOn(document, "createElement");
    await expect(convertHeicBlobToJpeg(new Blob())).rejects.toThrow(
      "Invalid HEIC",
    );
    expect(createCanvas).not.toHaveBeenCalled();
  });
});
