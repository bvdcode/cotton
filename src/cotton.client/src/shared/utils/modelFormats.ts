export type ModelFormat = "stl" | "obj" | "3mf" | "gltf" | "glb" | "ply" | "fbx";

const MODEL_FORMAT_BY_EXTENSION: Readonly<Record<string, ModelFormat>> = {
  stl: "stl",
  obj: "obj",
  "3mf": "3mf",
  gltf: "gltf",
  glb: "glb",
  ply: "ply",
  fbx: "fbx",
};

const MODEL_FORMAT_BY_MIME_TYPE: Readonly<Record<string, ModelFormat>> = {
  "model/stl": "stl",
  "application/sla": "stl",
  "application/vnd.ms-pki.stl": "stl",
  "model/x.stl-ascii": "stl",

  "model/obj": "obj",

  "model/3mf": "3mf",
  "application/vnd.ms-package.3dmanufacturing-3dmodel+xml": "3mf",

  "model/gltf+json": "gltf",
  "application/gltf+json": "gltf",

  "model/gltf-binary": "glb",

  "model/ply": "ply",
  "application/x-ply": "ply",

  "model/fbx": "fbx",
  "application/vnd.autodesk.fbx": "fbx",
};

const normalizeContentType = (contentType?: string | null): string => {
  if (!contentType) {
    return "";
  }

  return contentType.toLowerCase().split(";")[0]?.trim() ?? "";
};

const getLowercaseExtension = (fileName: string): string => {
  return fileName.toLowerCase().split(".").pop() ?? "";
};

export const resolveModelFormat = (
  fileName: string,
  contentType?: string | null,
): ModelFormat | null => {
  const normalizedContentType = normalizeContentType(contentType);
  if (normalizedContentType) {
    const formatFromContentType = MODEL_FORMAT_BY_MIME_TYPE[normalizedContentType];
    if (formatFromContentType) {
      return formatFromContentType;
    }
  }

  const extension = getLowercaseExtension(fileName);
  return MODEL_FORMAT_BY_EXTENSION[extension] ?? null;
};
