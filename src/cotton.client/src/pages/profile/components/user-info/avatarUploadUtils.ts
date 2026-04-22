import heic2any from "heic2any";

const AVATAR_PREFERRED_CHUNK_RATIO = 0.6;
const AVATAR_MIN_DIMENSION = 64;
const AVATAR_RESIZE_ATTEMPTS = 6;
const AVATAR_RESIZE_SCALE_STEP = 0.84;
const AVATAR_START_QUALITY = 0.92;
const AVATAR_MIN_QUALITY = 0.46;
const AVATAR_QUALITY_STEP = 0.1;
const HEIC_EXTENSION_REGEX = /\.(heic|heif)$/i;

export const AVATAR_FILE_ACCEPT =
  ".bmp,.gif,.heic,.heif,.jpeg,.jpg,.pbm,.png,.tiff,.tif,.tga,.webp,.qoi";

export class AvatarImageDecodeError extends Error {
  constructor() {
    super("Failed to decode image in browser.");
    this.name = "AvatarImageDecodeError";
  }
}

interface PreparedAvatar {
  blob: Blob;
  fileName: string;
}

const isHeicLikeFile = (file: File): boolean => {
  const normalizedType = file.type.toLowerCase();
  return (
    HEIC_EXTENSION_REGEX.test(file.name) ||
    normalizedType.startsWith("image/heic") ||
    normalizedType.startsWith("image/heif")
  );
};

const buildAvatarFileName = (sourceName: string): string => {
  const nameWithoutExt = sourceName.replace(/\.[^.]+$/, "").trim();
  const safeBaseName = nameWithoutExt.length > 0 ? nameWithoutExt : "avatar";
  return `${safeBaseName}.webp`;
};

const buildAvatarFileNameWithExtension = (
  sourceName: string,
  extension: string,
): string => {
  const nameWithoutExt = sourceName.replace(/\.[^.]+$/, "").trim();
  const safeBaseName = nameWithoutExt.length > 0 ? nameWithoutExt : "avatar";
  return `${safeBaseName}.${extension}`;
};

const loadImageFromBlob = async (blob: Blob): Promise<HTMLImageElement> => {
  return await new Promise((resolve, reject) => {
    const objectUrl = URL.createObjectURL(blob);
    const image = new Image();

    image.onload = () => {
      URL.revokeObjectURL(objectUrl);
      resolve(image);
    };

    image.onerror = () => {
      URL.revokeObjectURL(objectUrl);
      reject(new AvatarImageDecodeError());
    };

    image.src = objectUrl;
  });
};

const convertHeicBlobToJpeg = async (blob: Blob): Promise<Blob> => {
  const convertedUnknown: unknown = await heic2any({
    blob,
    toType: "image/jpeg",
    quality: AVATAR_START_QUALITY,
  });

  const converted = Array.isArray(convertedUnknown)
    ? convertedUnknown[0]
    : convertedUnknown;

  if (converted instanceof Blob) {
    return converted;
  }

  throw new AvatarImageDecodeError();
};

const decodeImageWithHeicFallback = async (
  file: File,
): Promise<{ blob: Blob; image: HTMLImageElement; fileName: string }> => {
  try {
    const image = await loadImageFromBlob(file);
    return {
      blob: file,
      image,
      fileName: file.name,
    };
  } catch (error) {
    if (!(error instanceof AvatarImageDecodeError) || !isHeicLikeFile(file)) {
      throw error;
    }

    const convertedBlob = await convertHeicBlobToJpeg(file);
    const image = await loadImageFromBlob(convertedBlob);

    return {
      blob: convertedBlob,
      image,
      fileName: buildAvatarFileNameWithExtension(file.name, "jpg"),
    };
  }
};

const canvasToWebpBlob = async (
  canvas: HTMLCanvasElement,
  quality: number,
): Promise<Blob> => {
  return await new Promise((resolve, reject) => {
    canvas.toBlob(
      (blob) => {
        if (!blob) {
          reject(new Error("Failed to create avatar blob."));
          return;
        }

        resolve(blob);
      },
      "image/webp",
      quality,
    );
  });
};

const tryFitAvatarToLimit = async (
  image: HTMLImageElement,
  maxBytes: number,
): Promise<Blob | null> => {
  if (maxBytes <= 0) {
    return null;
  }

  const canvas = document.createElement("canvas");
  const context = canvas.getContext("2d");
  if (!context) {
    return null;
  }

  let scale = 1;

  for (
    let resizeAttempt = 0;
    resizeAttempt < AVATAR_RESIZE_ATTEMPTS;
    resizeAttempt += 1
  ) {
    const targetWidth = Math.max(
      AVATAR_MIN_DIMENSION,
      Math.round(image.naturalWidth * scale),
    );
    const targetHeight = Math.max(
      AVATAR_MIN_DIMENSION,
      Math.round(image.naturalHeight * scale),
    );

    canvas.width = targetWidth;
    canvas.height = targetHeight;
    context.clearRect(0, 0, targetWidth, targetHeight);
    context.drawImage(image, 0, 0, targetWidth, targetHeight);

    for (
      let quality = AVATAR_START_QUALITY;
      quality >= AVATAR_MIN_QUALITY;
      quality -= AVATAR_QUALITY_STEP
    ) {
      const blob = await canvasToWebpBlob(canvas, Number(quality.toFixed(2)));
      if (blob.size <= maxBytes) {
        return blob;
      }
    }

    scale *= AVATAR_RESIZE_SCALE_STEP;
  }

  return null;
};

export const prepareAvatarForUpload = async (
  file: File,
  maxChunkSizeBytes: number,
): Promise<PreparedAvatar | null> => {
  if (file.size <= maxChunkSizeBytes && !isHeicLikeFile(file)) {
    return {
      blob: file,
      fileName: file.name,
    };
  }

  const decoded = await decodeImageWithHeicFallback(file);
  if (decoded.blob.size <= maxChunkSizeBytes) {
    return {
      blob: decoded.blob,
      fileName: decoded.fileName,
    };
  }

  const preferredTargetBytes = Math.max(
    AVATAR_MIN_DIMENSION,
    Math.floor(maxChunkSizeBytes * AVATAR_PREFERRED_CHUNK_RATIO),
  );

  const primaryTargetBytes =
    decoded.blob.size > preferredTargetBytes
      ? preferredTargetBytes
      : maxChunkSizeBytes;

  const primaryBlob = await tryFitAvatarToLimit(decoded.image, primaryTargetBytes);
  if (primaryBlob) {
    return {
      blob: primaryBlob,
      fileName: buildAvatarFileName(file.name),
    };
  }

  if (primaryTargetBytes !== maxChunkSizeBytes) {
    const maxSizeBlob = await tryFitAvatarToLimit(
      decoded.image,
      maxChunkSizeBytes,
    );
    if (!maxSizeBlob) {
      return null;
    }

    return {
      blob: maxSizeBlob,
      fileName: buildAvatarFileName(file.name),
    };
  }

  return null;
};
