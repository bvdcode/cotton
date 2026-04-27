import * as THREE from "three";
import { filesApi } from "../../../../shared/api/filesApi";
import { type ModelFormat } from "../../utils/modelFormats";
import {
  type FlipOrientationVariant,
  type LightingPresetConfig,
  type MaterialSurfaceState,
  type MeshStoredMaterial,
  type ModelLightingPreset,
  type ModelSurfacePreset,
  type PreparedModelScene,
  type PreviewQualityMode,
} from "./modelPreviewTypes";

const EXPIRE_AFTER_MINUTES = 60 * 24;
const LARGE_MODEL_FILE_THRESHOLD_BYTES = 20 * 1024 * 1024;
const TARGET_MODEL_MAX_DIMENSION = 4;
const MIN_GRID_SIZE = 6;
const MAX_GRID_SIZE = 48;
const GRID_SIZE_MULTIPLIER = 2.4;
const GRID_DENSITY_FACTOR = 4;
const MIN_GRID_DIVISIONS = 20;
const MAX_GRID_DIVISIONS = 120;
const LOW_QUALITY_REDUCTION_RATIO = 0.45;
const LOW_QUALITY_TARGET_MAX_VERTICES = 450_000;
const LOW_QUALITY_MIN_TARGET_VERTICES = 120_000;
const QUARTER_TURN = Math.PI / 2;

const AUTO_ORIENT_VARIANTS: ReadonlyArray<FlipOrientationVariant> = [
  { quaternion: new THREE.Quaternion() },
  {
    quaternion: new THREE.Quaternion().setFromAxisAngle(
      new THREE.Vector3(1, 0, 0),
      Math.PI,
    ),
  },
  {
    quaternion: new THREE.Quaternion().setFromAxisAngle(
      new THREE.Vector3(0, 0, 1),
      QUARTER_TURN,
    ),
  },
  {
    quaternion: new THREE.Quaternion().setFromAxisAngle(
      new THREE.Vector3(0, 0, 1),
      -QUARTER_TURN,
    ),
  },
  {
    quaternion: new THREE.Quaternion().setFromAxisAngle(
      new THREE.Vector3(1, 0, 0),
      QUARTER_TURN,
    ),
  },
  {
    quaternion: new THREE.Quaternion().setFromAxisAngle(
      new THREE.Vector3(1, 0, 0),
      -QUARTER_TURN,
    ),
  },
];

export const MANUAL_FLIP_ORIENTATION_VARIANTS: ReadonlyArray<FlipOrientationVariant> =
  AUTO_ORIENT_VARIANTS;

export const LIGHTING_PRESET_CONFIG: Record<
  ModelLightingPreset,
  LightingPresetConfig
> = {
  balanced: {
    ambientIntensity: 0.34,
    keyIntensity: 0.78,
    fillIntensity: 0.1,
    rimIntensity: 0.26,
  },
  studio: {
    ambientIntensity: 0.4,
    keyIntensity: 0.86,
    fillIntensity: 0.14,
    rimIntensity: 0.3,
  },
  dramatic: {
    ambientIntensity: 0.32,
    keyIntensity: 1.02,
    fillIntensity: 0.12,
    rimIntensity: 0.44,
  },
};

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

const disposeMaterial = (material: THREE.Material): void => {
  material.dispose();
};

const disposeStoredMaterial = (material: MeshStoredMaterial): void => {
  if (Array.isArray(material)) {
    material.forEach(disposeMaterial);
    return;
  }

  disposeMaterial(material);
};

export const disposeObject3D = (object: THREE.Object3D): void => {
  object.traverse((node) => {
    if (!(node instanceof THREE.Mesh)) {
      return;
    }

    node.geometry?.dispose();

    if (Array.isArray(node.material)) {
      node.material.forEach(disposeMaterial);
      return;
    }

    if (node.material) {
      disposeMaterial(node.material);
    }
  });
};

const clamp = (value: number, min: number, max: number): number => {
  return Math.min(Math.max(value, min), max);
};

const simplifyObjectGeometry = async (
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

const hasColorProperty = (
  material: THREE.Material,
): material is THREE.Material & { color: THREE.Color } => {
  const materialWithColor = material as THREE.Material & { color?: THREE.Color };
  return "color" in material && materialWithColor.color instanceof THREE.Color;
};

const hasStandardSurfaceProperties = (
  material: THREE.Material,
): material is THREE.Material & { metalness: number; roughness: number } => {
  const candidate = material as THREE.Material & {
    metalness?: number;
    roughness?: number;
  };

  return typeof candidate.metalness === "number" &&
    typeof candidate.roughness === "number";
};

const hasEnvMapIntensity = (
  material: THREE.Material,
): material is THREE.Material & { envMapIntensity: number } => {
  const candidate = material as THREE.Material & { envMapIntensity?: number };
  return typeof candidate.envMapIntensity === "number";
};

const hasPhongShininess = (
  material: THREE.Material,
): material is THREE.Material & { shininess: number } => {
  const candidate = material as THREE.Material & { shininess?: number };
  return typeof candidate.shininess === "number";
};

const hasPhongReflectivity = (
  material: THREE.Material,
): material is THREE.Material & { reflectivity: number } => {
  const candidate = material as THREE.Material & { reflectivity?: number };
  return typeof candidate.reflectivity === "number";
};

const hasPhysicalSurfaceProperties = (
  material: THREE.Material,
): material is THREE.Material & {
  clearcoat: number;
  clearcoatRoughness: number;
} => {
  const candidate = material as THREE.Material & {
    clearcoat?: number;
    clearcoatRoughness?: number;
  };

  return typeof candidate.clearcoat === "number" &&
    typeof candidate.clearcoatRoughness === "number";
};

const hasFlatShadingProperty = (
  material: THREE.Material,
): material is THREE.Material & { flatShading: boolean } => {
  const candidate = material as THREE.Material & { flatShading?: boolean };
  return typeof candidate.flatShading === "boolean";
};

const createPreviewOverrideMaterial = (
  material: THREE.Material,
): THREE.MeshPhysicalMaterial => {
  const previewMaterial = new THREE.MeshPhysicalMaterial({
    color: new THREE.Color("#b8b8b8"),
    metalness: 0,
    roughness: 0.72,
    side: material.side,
    transparent: material.transparent,
    opacity: material.opacity,
    alphaTest: material.alphaTest,
    depthTest: material.depthTest,
    depthWrite: material.depthWrite,
    visible: material.visible,
    flatShading: hasFlatShadingProperty(material)
      ? material.flatShading
      : false,
  });

  previewMaterial.name = material.name
    ? `${material.name}-preview-override`
    : "preview-override";

  return previewMaterial;
};

export const applyPreviewOverrideMaterials = (
  object: THREE.Object3D,
  shouldOverride: boolean,
  originalMaterialMap: WeakMap<THREE.Mesh, MeshStoredMaterial>,
): void => {
  object.traverse((node) => {
    if (!(node instanceof THREE.Mesh) || !node.material) {
      return;
    }

    const savedOriginalMaterial = originalMaterialMap.get(node);

    if (shouldOverride) {
      if (!savedOriginalMaterial) {
        originalMaterialMap.set(node, node.material);
        node.material = Array.isArray(node.material)
          ? node.material.map(createPreviewOverrideMaterial)
          : createPreviewOverrideMaterial(node.material);
      }

      return;
    }

    if (!savedOriginalMaterial) {
      return;
    }

    disposeStoredMaterial(node.material);
    node.material = savedOriginalMaterial;
  });
};

const resolveCssVariableColor = (value: string): string => {
  const trimmed = value.trim();
  if (!trimmed.startsWith("var(") || typeof window === "undefined") {
    return trimmed;
  }

  const match = trimmed.match(/^var\((--[^,\s)]+)(?:,\s*([^)]*))?\)$/);
  if (!match) {
    return trimmed;
  }

  const variableName = match[1];
  const fallbackValue = match[2]?.trim();
  const resolved = window
    .getComputedStyle(document.documentElement)
    .getPropertyValue(variableName)
    .trim();

  if (resolved) {
    return resolved;
  }

  return fallbackValue && fallbackValue.length > 0 ? fallbackValue : trimmed;
};

const resolveComputedCssColor = (value: string): string => {
  if (typeof window === "undefined" || typeof document === "undefined") {
    return value;
  }

  if (!document.body) {
    return value;
  }

  const probe = document.createElement("span");
  probe.style.color = "";
  probe.style.color = value;

  if (!probe.style.color) {
    return value;
  }

  document.body.appendChild(probe);
  const computed = window.getComputedStyle(probe).color.trim();
  probe.remove();

  return computed || value;
};

const toThreeColor = (colorValue: string): THREE.Color | null => {
  const resolvedColor = resolveCssVariableColor(colorValue);
  const computedColor = resolveComputedCssColor(resolvedColor);
  const parsedColor = new THREE.Color();

  try {
    parsedColor.set(computedColor);
    return parsedColor;
  } catch {
    return null;
  }
};

export const applyMaterialColor = (
  object: THREE.Object3D,
  color: string | null | undefined,
  originalColors: WeakMap<THREE.Material, THREE.Color>,
): void => {
  const overrideColor = color ? toThreeColor(color) : null;

  object.traverse((node) => {
    if (!(node instanceof THREE.Mesh)) {
      return;
    }

    const applyToMaterial = (material: THREE.Material): void => {
      if (!hasColorProperty(material)) {
        return;
      }

      if (!originalColors.has(material)) {
        originalColors.set(material, material.color.clone());
      }

      if (overrideColor) {
        material.color.copy(overrideColor);
      } else {
        const original = originalColors.get(material);
        if (original) {
          material.color.copy(original);
        }
      }

      material.needsUpdate = true;
    };

    if (Array.isArray(node.material)) {
      node.material.forEach(applyToMaterial);
      return;
    }

    if (node.material) {
      applyToMaterial(node.material);
    }
  });
};

export const applyMaterialSurfacePreset = (
  object: THREE.Object3D,
  surfacePreset: ModelSurfacePreset,
  originalSurfaceMap: WeakMap<THREE.Material, MaterialSurfaceState>,
  hasColorOverride: boolean,
): void => {
  object.traverse((node) => {
    if (!(node instanceof THREE.Mesh)) {
      return;
    }

    const applyToMaterial = (material: THREE.Material): void => {
      if (!originalSurfaceMap.has(material)) {
        const state: MaterialSurfaceState = {};

        if (hasStandardSurfaceProperties(material)) {
          state.metalness = material.metalness;
          state.roughness = material.roughness;
        }

        if (hasEnvMapIntensity(material)) {
          state.envMapIntensity = material.envMapIntensity;
        }

        if (hasPhongShininess(material)) {
          state.shininess = material.shininess;
        }

        if (hasPhongReflectivity(material)) {
          state.reflectivity = material.reflectivity;
        }

        if (hasPhysicalSurfaceProperties(material)) {
          state.clearcoat = material.clearcoat;
          state.clearcoatRoughness = material.clearcoatRoughness;
        }

        if (hasFlatShadingProperty(material)) {
          state.flatShading = material.flatShading;
        }

        originalSurfaceMap.set(material, state);
      }

      const originalState = originalSurfaceMap.get(material);
      if (!originalState) {
        return;
      }

      if (hasStandardSurfaceProperties(material)) {
        switch (surfacePreset) {
          case "metal":
            material.metalness = hasColorOverride ? 1 : 0.82;
            material.roughness = hasColorOverride ? 0.18 : 0.38;
            break;
          case "smooth":
            material.metalness = 0.02;
            material.roughness = hasColorOverride ? 0.4 : 0.52;
            break;
          case "original":
          default:
            if (hasColorOverride) {
              material.metalness = 0;
              material.roughness = 0.72;
            } else {
              if (typeof originalState.metalness === "number") {
                material.metalness = originalState.metalness;
              }
              if (typeof originalState.roughness === "number") {
                material.roughness = originalState.roughness;
              }
            }
            break;
        }
      }

      if (hasEnvMapIntensity(material)) {
        switch (surfacePreset) {
          case "metal":
            material.envMapIntensity = hasColorOverride ? 1.1 : 0.78;
            break;
          case "smooth":
            material.envMapIntensity = hasColorOverride ? 0.08 : 0.22;
            break;
          case "original":
          default:
            if (hasColorOverride) {
              material.envMapIntensity = 0;
            } else if (typeof originalState.envMapIntensity === "number") {
              material.envMapIntensity = originalState.envMapIntensity;
            }
            break;
        }
      }

      if (hasPhongShininess(material)) {
        switch (surfacePreset) {
          case "metal":
            material.shininess = hasColorOverride ? 150 : 120;
            break;
          case "smooth":
            material.shininess = hasColorOverride ? 42 : 36;
            break;
          case "original":
          default:
            if (hasColorOverride) {
              material.shininess = 14;
            } else if (typeof originalState.shininess === "number") {
              material.shininess = originalState.shininess;
            }
            break;
        }
      }

      if (hasPhongReflectivity(material)) {
        switch (surfacePreset) {
          case "metal":
            material.reflectivity = hasColorOverride ? 0.95 : 0.58;
            break;
          case "smooth":
            material.reflectivity = hasColorOverride ? 0.12 : 0.2;
            break;
          case "original":
          default:
            if (hasColorOverride) {
              material.reflectivity = 0.03;
            } else if (typeof originalState.reflectivity === "number") {
              material.reflectivity = originalState.reflectivity;
            }
            break;
        }
      }

      if (hasPhysicalSurfaceProperties(material)) {
        switch (surfacePreset) {
          case "metal":
            material.clearcoat = hasColorOverride ? 0.22 : 0.14;
            material.clearcoatRoughness = hasColorOverride ? 0.18 : 0.42;
            break;
          case "smooth":
            material.clearcoat = hasColorOverride ? 0.05 : 0.08;
            material.clearcoatRoughness = hasColorOverride ? 0.62 : 0.58;
            break;
          case "original":
          default:
            if (hasColorOverride) {
              material.clearcoat = 0;
              material.clearcoatRoughness = 1;
            } else {
              if (typeof originalState.clearcoat === "number") {
                material.clearcoat = originalState.clearcoat;
              }
              if (typeof originalState.clearcoatRoughness === "number") {
                material.clearcoatRoughness = originalState.clearcoatRoughness;
              }
            }
            break;
        }
      }

      if (hasFlatShadingProperty(material)) {
        switch (surfacePreset) {
          case "smooth":
            material.flatShading = false;
            break;
          case "metal":
          case "original":
          default:
            if (typeof originalState.flatShading === "boolean") {
              material.flatShading = originalState.flatShading;
            }
            break;
        }
      }

      material.needsUpdate = true;
    };

    if (Array.isArray(node.material)) {
      node.material.forEach(applyToMaterial);
      return;
    }

    if (node.material) {
      applyToMaterial(node.material);
    }
  });
};

export const applyShadowPreferences = (
  object: THREE.Object3D,
  shadowsEnabled: boolean,
): void => {
  object.traverse((node) => {
    if (!(node instanceof THREE.Mesh)) {
      return;
    }

    node.castShadow = shadowsEnabled;
    node.receiveShadow = shadowsEnabled;
  });
};

const ensureMeshNormals = (object: THREE.Object3D): void => {
  object.traverse((node) => {
    if (!(node instanceof THREE.Mesh)) {
      return;
    }

    const geometry = node.geometry;
    if (!(geometry instanceof THREE.BufferGeometry)) {
      return;
    }

    if (!geometry.getAttribute("normal")) {
      geometry.computeVertexNormals();
    }
  });
};

const orientLongestAxisUp = (object: THREE.Object3D): void => {
  object.updateMatrixWorld(true);

  const bounds = new THREE.Box3().setFromObject(object);
  if (bounds.isEmpty()) {
    return;
  }

  const size = bounds.getSize(new THREE.Vector3());

  if (size.x > size.y && size.x >= size.z) {
    object.rotateZ(Math.PI / 2);
  } else if (size.z > size.y && size.z >= size.x) {
    object.rotateX(-Math.PI / 2);
  }

  object.updateMatrixWorld(true);
};

const calculateSupportScore = (object: THREE.Object3D): number => {
  object.updateMatrixWorld(true);

  const bounds = new THREE.Box3().setFromObject(object);
  if (bounds.isEmpty()) {
    return 0;
  }

  const height = Math.max(bounds.max.y - bounds.min.y, 0.0001);
  const footprintWidth = Math.max(bounds.max.x - bounds.min.x, 0.0001);
  const footprintDepth = Math.max(bounds.max.z - bounds.min.z, 0.0001);
  const footprintArea = footprintWidth * footprintDepth;
  const footprintDiagonal = Math.hypot(footprintWidth, footprintDepth);
  const floorThreshold = bounds.min.y + Math.max(height * 0.02, 0.0005);
  const boundsCenter = bounds.getCenter(new THREE.Vector3());

  const vertex = new THREE.Vector3();
  let sampledVertices = 0;
  let supportVertices = 0;
  let supportMinX = Number.POSITIVE_INFINITY;
  let supportMaxX = Number.NEGATIVE_INFINITY;
  let supportMinZ = Number.POSITIVE_INFINITY;
  let supportMaxZ = Number.NEGATIVE_INFINITY;
  let supportSumX = 0;
  let supportSumZ = 0;

  object.traverse((node) => {
    if (!(node instanceof THREE.Mesh)) {
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

    const step = Math.max(1, Math.floor(position.count / 2000));
    for (let index = 0; index < position.count; index += step) {
      vertex
        .fromBufferAttribute(position, index)
        .applyMatrix4(node.matrixWorld);

      sampledVertices += 1;
      if (vertex.y <= floorThreshold) {
        supportVertices += 1;
        supportMinX = Math.min(supportMinX, vertex.x);
        supportMaxX = Math.max(supportMaxX, vertex.x);
        supportMinZ = Math.min(supportMinZ, vertex.z);
        supportMaxZ = Math.max(supportMaxZ, vertex.z);
        supportSumX += vertex.x;
        supportSumZ += vertex.z;
      }
    }
  });

  if (sampledVertices === 0 || supportVertices === 0) {
    return 0;
  }

  const supportRatio = supportVertices / sampledVertices;
  const supportWidth = Math.max(supportMaxX - supportMinX, 0);
  const supportDepth = Math.max(supportMaxZ - supportMinZ, 0);
  const supportAreaRatio = clamp(
    (supportWidth * supportDepth) / footprintArea,
    0,
    1,
  );
  const supportCenterOffset = Math.hypot(
    supportSumX / supportVertices - boundsCenter.x,
    supportSumZ / supportVertices - boundsCenter.z,
  );
  const supportCenterScore = 1 - clamp(
    supportCenterOffset / Math.max(footprintDiagonal * 0.5, 0.0001),
    0,
    1,
  );

  return supportRatio * 0.5 + supportAreaRatio * 0.35 + supportCenterScore * 0.15;
};

export const autoOrientModelUpright = (object: THREE.Object3D): void => {
  orientLongestAxisUp(object);

  const baseQuaternion = object.quaternion.clone();
  let bestSupportScore = Number.NEGATIVE_INFINITY;
  const bestQuaternion = baseQuaternion.clone();

  for (const orientationVariant of AUTO_ORIENT_VARIANTS) {
    object.quaternion
      .copy(baseQuaternion)
      .multiply(orientationVariant.quaternion);

    object.updateMatrixWorld(true);
    const supportScore = calculateSupportScore(object);
    if (supportScore > bestSupportScore) {
      bestSupportScore = supportScore;
      bestQuaternion.copy(object.quaternion);
    }
  }

  object.quaternion.copy(bestQuaternion);
  object.updateMatrixWorld(true);
};

export const applyFlipOrientation = (
  object: THREE.Object3D,
  baseQuaternion: THREE.Quaternion,
  orientationVariant: FlipOrientationVariant,
): void => {
  object.quaternion.copy(baseQuaternion).multiply(orientationVariant.quaternion);
  object.updateMatrixWorld(true);
};

const normalizeModelScale = (object: THREE.Object3D): void => {
  object.updateMatrixWorld(true);

  const bounds = new THREE.Box3().setFromObject(object);
  if (bounds.isEmpty()) {
    return;
  }

  const size = bounds.getSize(new THREE.Vector3());
  const maxDimension = Math.max(size.x, size.y, size.z);
  if (!Number.isFinite(maxDimension) || maxDimension <= 0) {
    return;
  }

  const normalizedScale = TARGET_MODEL_MAX_DIMENSION / maxDimension;
  object.scale.multiplyScalar(normalizedScale);
  object.updateMatrixWorld(true);
};

const loadModelObject = async (
  format: ModelFormat,
  url: string,
): Promise<THREE.Object3D> => {
  switch (format) {
    case "stl": {
      const { STLLoader } = await import("three/examples/jsm/loaders/STLLoader.js");
      const geometry = await new STLLoader().loadAsync(url);
      geometry.computeVertexNormals();
      return new THREE.Mesh(geometry, createNeutralMaterial());
    }

    case "ply": {
      const { PLYLoader } = await import("three/examples/jsm/loaders/PLYLoader.js");
      const geometry = await new PLYLoader().loadAsync(url);
      geometry.computeVertexNormals();
      return new THREE.Mesh(geometry, createNeutralMaterial());
    }

    case "obj": {
      const { OBJLoader } = await import("three/examples/jsm/loaders/OBJLoader.js");
      return new OBJLoader().loadAsync(url);
    }

    case "fbx": {
      const { FBXLoader } = await import("three/examples/jsm/loaders/FBXLoader.js");
      return new FBXLoader().loadAsync(url);
    }

    case "3mf": {
      const { ThreeMFLoader } = await import("three/examples/jsm/loaders/3MFLoader.js");
      return new ThreeMFLoader().loadAsync(url);
    }

    case "gltf":
    case "glb": {
      const { GLTFLoader } = await import("three/examples/jsm/loaders/GLTFLoader.js");
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

export const alignModelToGround = (object: THREE.Object3D): void => {
  object.updateMatrixWorld(true);

  const bounds = new THREE.Box3().setFromObject(object);
  if (bounds.isEmpty()) {
    return;
  }

  const center = bounds.getCenter(new THREE.Vector3());
  const minY = bounds.min.y;

  object.position.x -= center.x;
  object.position.z -= center.z;
  object.position.y -= minY;
  object.updateMatrixWorld(true);
};

export const buildGridMetrics = (
  object: THREE.Object3D,
  qualityMode: PreviewQualityMode,
): {
  gridSize: number;
  gridDivisions: number;
} => {
  object.updateMatrixWorld(true);

  const bounds = new THREE.Box3().setFromObject(object);
  if (bounds.isEmpty()) {
    return {
      gridSize: MIN_GRID_SIZE,
      gridDivisions: MIN_GRID_DIVISIONS,
    };
  }

  const size = bounds.getSize(new THREE.Vector3());
  const footprint = Math.max(size.x, size.z);
  const gridSizeMultiplier =
    qualityMode === "reduced"
      ? GRID_SIZE_MULTIPLIER * 1.2
      : GRID_SIZE_MULTIPLIER;
  const densityFactor =
    qualityMode === "reduced"
      ? GRID_DENSITY_FACTOR * 0.7
      : GRID_DENSITY_FACTOR;

  const gridSize = clamp(
    footprint * gridSizeMultiplier,
    MIN_GRID_SIZE,
    MAX_GRID_SIZE,
  );

  const gridDivisions = Math.round(
    clamp(
      gridSize * densityFactor,
      MIN_GRID_DIVISIONS,
      MAX_GRID_DIVISIONS,
    ),
  );

  return {
    gridSize,
    gridDivisions,
  };
};

export const prepareModelScene = async (
  object: THREE.Object3D,
  qualityMode: PreviewQualityMode,
): Promise<PreparedModelScene> => {
  await simplifyObjectGeometry(object, qualityMode);
  ensureMeshNormals(object);
  normalizeModelScale(object);
  alignModelToGround(object);

  const metrics = buildGridMetrics(object, qualityMode);
  return {
    object,
    gridSize: metrics.gridSize,
    gridDivisions: metrics.gridDivisions,
    qualityMode,
  };
};

export const loadPreparedModelScene = async (
  modelFormat: ModelFormat,
  sourceKey: string,
  qualityMode: PreviewQualityMode,
): Promise<PreparedModelScene> => {
  const url = await resolveSourceUrlByKey(sourceKey);
  const loadedObject = await loadModelObject(modelFormat, url);
  return prepareModelScene(loadedObject, qualityMode);
};