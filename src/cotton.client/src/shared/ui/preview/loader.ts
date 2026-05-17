import * as THREE from "three";
import { filesApi } from "@shared/api/filesApi";
import { type ModelFormat } from "@shared/utils/modelFormats";
import type { PreviewQualityMode } from "./modelPreviewTypes";

/**
 * Loader-side concerns: format-specific three.js loaders, URL resolution
 * (filesApi or direct URL), the file-size driven quality heuristic and
 * the optional simplification pass used in 'reduced' quality mode.
 */

const EXPIRE_AFTER_MINUTES = 60 * 24;
const LARGE_MODEL_FILE_THRESHOLD_BYTES = 20 * 1024 * 1024;
const LOW_QUALITY_REDUCTION_RATIO = 0.45;
const LOW_QUALITY_TARGET_MAX_VERTICES = 450_000;
const LOW_QUALITY_MIN_TARGET_VERTICES = 120_000;

export const resolveQualityMode = (
  fileSizeBytes?: number | null,
): PreviewQualityMode => {
  return typeof fileSizeBytes === "number" &&
    fileSizeBytes >= LARGE_MODEL_FILE_THRESHOLD_BYTES
    ? "reduced"
    : "normal";
};

const toAbsoluteUrl = (url: string): string => {
  if (typeof window === "undefined") {
    return url;
  }

  return new URL(url, window.location.origin).toString();
};

const toInlineDownloadUrl = (url: string): string => {
  const absolute = toAbsoluteUrl(url);
  const normalized = new URL(absolute);
  normalized.searchParams.set("download", "false");
  return normalized.toString();
};

const createNeutralMaterial = (): THREE.MeshStandardMaterial => {
  return new THREE.MeshStandardMaterial({
    metalness: 0.08,
    roughness: 0.7,
    side: THREE.DoubleSide,
  });
};

export const simplifyObjectGeometry = async (
  object: THREE.Object3D,
  qualityMode: PreviewQualityMode,
): Promise<void> => {
  if (qualityMode !== "reduced") {
    return;
  }

  const { SimplifyModifier } = await import(
    "three/examples/jsm/modifiers/SimplifyModifier.js"
  );
  const modifier = new SimplifyModifier();

  object.traverse((node) => {
    if (!(node instanceof THREE.Mesh) || node instanceof THREE.SkinnedMesh) {
      return;
    }

    const geometry = node.geometry;
    if (!(geometry instanceof THREE.BufferGeometry)) {
      return;
    }

    const position = geometry.getAttribute("position");
    if (!position) {
      return;
    }

    const currentVertexCount = position.count;
    if (currentVertexCount <= LOW_QUALITY_TARGET_MAX_VERTICES) {
      return;
    }

    const targetVertexCount = Math.max(
      LOW_QUALITY_MIN_TARGET_VERTICES,
      Math.min(
        LOW_QUALITY_TARGET_MAX_VERTICES,
        Math.floor(currentVertexCount * LOW_QUALITY_REDUCTION_RATIO),
      ),
    );

    const verticesToRemove = currentVertexCount - targetVertexCount;
    if (verticesToRemove <= 0) {
      return;
    }

    try {
      const simplified = modifier.modify(geometry, verticesToRemove);
      simplified.computeVertexNormals();

      geometry.dispose();
      node.geometry = simplified;
    } catch {
      // Keep original geometry if simplification fails for a specific mesh.
    }
  });
};

const loadModelObject = async (
  format: ModelFormat,
  url: string,
): Promise<THREE.Object3D> => {
  switch (format) {
    case "stl": {
      const { STLLoader } = await import(
        "three/examples/jsm/loaders/STLLoader.js"
      );
      const geometry = await new STLLoader().loadAsync(url);
      geometry.computeVertexNormals();
      return new THREE.Mesh(geometry, createNeutralMaterial());
    }

    case "ply": {
      const { PLYLoader } = await import(
        "three/examples/jsm/loaders/PLYLoader.js"
      );
      const geometry = await new PLYLoader().loadAsync(url);
      geometry.computeVertexNormals();
      return new THREE.Mesh(geometry, createNeutralMaterial());
    }

    case "obj": {
      const { OBJLoader } = await import(
        "three/examples/jsm/loaders/OBJLoader.js"
      );
      return new OBJLoader().loadAsync(url);
    }

    case "fbx": {
      const { FBXLoader } = await import(
        "three/examples/jsm/loaders/FBXLoader.js"
      );
      return new FBXLoader().loadAsync(url);
    }

    case "3mf": {
      const { ThreeMFLoader } = await import(
        "three/examples/jsm/loaders/3MFLoader.js"
      );
      return new ThreeMFLoader().loadAsync(url);
    }

    case "gltf":
    case "glb": {
      const { GLTFLoader } = await import(
        "three/examples/jsm/loaders/GLTFLoader.js"
      );
      const gltf = await new GLTFLoader().loadAsync(url);
      return gltf.scene;
    }

    default: {
      throw new Error(`Unsupported model format: ${format}`);
    }
  }
};

const resolveSourceUrlByKey = async (sourceKey: string): Promise<string> => {
  if (sourceKey.startsWith("url:")) {
    return toAbsoluteUrl(sourceKey.slice(4));
  }

  const fileId = sourceKey.slice(5);
  const downloadUrl = await filesApi.getDownloadLink(
    fileId,
    EXPIRE_AFTER_MINUTES,
  );
  return toInlineDownloadUrl(downloadUrl);
};

export const loadModelObjectFromSource = async (
  modelFormat: ModelFormat,
  sourceKey: string,
): Promise<THREE.Object3D> => {
  const url = await resolveSourceUrlByKey(sourceKey);
  return loadModelObject(modelFormat, url);
};
