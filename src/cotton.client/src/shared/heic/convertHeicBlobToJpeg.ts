import { HeicDecoder, type HeicImage } from "heic-net";
import { reportClientError } from "../utils/clientDiagnostics";

const decoder = new HeicDecoder({
  assetsUrl: new URL(
    `${import.meta.env.BASE_URL}heic/`,
    window.location.origin,
  ),
});
const JPEG_QUALITY = 0.92;

export const convertHeicBlobToJpeg = async (blob: Blob): Promise<Blob> => {
  let image: HeicImage;
  try {
    image = await decoder.decode(blob);
  } catch (error) {
    reportClientError("HEIC image decoding failed.", error);
    throw error;
  }
  const canvas = document.createElement("canvas");
  canvas.width = image.width;
  canvas.height = image.height;
  const context = canvas.getContext("2d");
  if (!context) {
    throw new Error("The browser could not create an image canvas.");
  }
  context.putImageData(
    new ImageData(image.pixels, image.width, image.height),
    0,
    0,
  );
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
