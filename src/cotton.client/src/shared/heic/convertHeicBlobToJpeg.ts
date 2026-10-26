import { HeicDecoderClient } from "./HeicDecoderClient";

const decoder = new HeicDecoderClient();
const JPEG_QUALITY = 0.92;

export const convertHeicBlobToJpeg = async (blob: Blob): Promise<Blob> => {
  const image = await decoder.decode(blob);
  const canvas = document.createElement("canvas");
  canvas.width = image.width;
  canvas.height = image.height;
  const context = canvas.getContext("2d");
  if (!context) {
    throw new Error("The browser could not create an image canvas.");
  }
  const pixels = new Uint8ClampedArray(image.pixels);
  context.putImageData(new ImageData(pixels, image.width, image.height), 0, 0);
  return new Promise((resolve, reject) => {
    canvas.toBlob(
      (converted) => {
        canvas.width = 0;
        canvas.height = 0;
        if (converted) {
          resolve(converted);
        } else {
          reject(
            new Error("The browser could not encode the HEIC image as JPEG."),
          );
        }
      },
      "image/jpeg",
      JPEG_QUALITY,
    );
  });
};
