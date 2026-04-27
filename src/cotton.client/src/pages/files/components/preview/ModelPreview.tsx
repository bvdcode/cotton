import * as React from "react";
import { Box, CircularProgress, Typography } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import { Canvas } from "@react-three/fiber";
import { Bounds, Environment, OrbitControls } from "@react-three/drei";
import { useTranslation } from "react-i18next";
import * as THREE from "three";
import { filesApi } from "../../../../shared/api/filesApi";
import { resolveModelFormat, type ModelFormat } from "../../utils/modelFormats";

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
const FLIP_ROTATION_STEPS: ReadonlyArray<number> = [
  0,
  QUARTER_TURN,
  Math.PI,
  QUARTER_TURN * 3,
];
const FLIP_ORIENTATION_VARIANTS: ReadonlyArray<FlipOrientationVariant> =
  FLIP_ROTATION_STEPS.flatMap((x) => {
    return FLIP_ROTATION_STEPS.map((z) => ({ x, z }));
  });

const LIGHTING_PRESET_CONFIG: Record<ModelLightingPreset, LightingPresetConfig> = {
  balanced: {
    ambientIntensity: 0.52,
    keyIntensity: 0.72,
    fillIntensity: 0.24,
    rimIntensity: 0.22,
  },
  studio: {
    ambientIntensity: 0.62,
    keyIntensity: 0.88,
    fillIntensity: 0.36,
    rimIntensity: 0.3,
  },
  dramatic: {
    ambientIntensity: 0.32,
    keyIntensity: 1.02,
    fillIntensity: 0.12,
    rimIntensity: 0.44,
  },
};

type ModelPreviewSource =
  | {
      kind: "fileId";
      fileId: string;
    }
  | {
      kind: "url";
      url: string;
    };

type PreviewQualityMode = "normal" | "reduced";
type ModelLightingPreset = "balanced" | "studio" | "dramatic";
type ModelSurfacePreset =
  | "original"
  | "metal"
  | "smooth";

interface ModelPreviewProps {
  source: ModelPreviewSource;
  fileName: string;
  contentType?: string | null;
  fileSizeBytes?: number | null;
  materialColor?: string | null;
  autoAlignToken?: number;
  autoOrientToken?: number;
  flipToken?: number;
  lightingPreset?: ModelLightingPreset;
  shadowsEnabled?: boolean;
  surfacePreset?: ModelSurfacePreset;
}

interface PreparedModelScene {
  object: THREE.Object3D;
  gridSize: number;
  gridDivisions: number;
  qualityMode: PreviewQualityMode;
}

interface LightingPresetConfig {
  ambientIntensity: number;
  keyIntensity: number;
  fillIntensity: number;
  rimIntensity: number;
}

interface FlipOrientationVariant {
  x: number;
  z: number;
}

interface MaterialSurfaceState {
  metalness?: number;
  roughness?: number;
  envMapIntensity?: number;
  shininess?: number;
  reflectivity?: number;
  clearcoat?: number;
  clearcoatRoughness?: number;
  flatShading?: boolean;
}

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

const disposeObject3D = (object: THREE.Object3D): void => {
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

const resolveQualityMode = (
  fileSizeBytes?: number | null,
): PreviewQualityMode => {
  return typeof fileSizeBytes === "number" &&
    fileSizeBytes >= LARGE_MODEL_FILE_THRESHOLD_BYTES
    ? "reduced"
    : "normal";
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

const applyMaterialColor = (
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

const applyMaterialSurfacePreset = (
  object: THREE.Object3D,
  surfacePreset: ModelSurfacePreset,
  originalSurfaceMap: WeakMap<THREE.Material, MaterialSurfaceState>,
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
            material.metalness = 1;
            material.roughness = 0.08;
            break;
          case "smooth":
            material.metalness = 0.04;
            material.roughness = 0.6;
            break;
          case "original":
          default:
            if (typeof originalState.metalness === "number") {
              material.metalness = originalState.metalness;
            }
            if (typeof originalState.roughness === "number") {
              material.roughness = originalState.roughness;
            }
            break;
        }
      }

      if (hasEnvMapIntensity(material)) {
        switch (surfacePreset) {
          case "metal":
            material.envMapIntensity = 1.8;
            break;
          case "smooth":
            material.envMapIntensity = 0.16;
            break;
          case "original":
          default:
            if (typeof originalState.envMapIntensity === "number") {
              material.envMapIntensity = originalState.envMapIntensity;
            }
            break;
        }
      }

      if (hasPhongShininess(material)) {
        switch (surfacePreset) {
          case "metal":
            material.shininess = 300;
            break;
          case "smooth":
            material.shininess = 28;
            break;
          case "original":
          default:
            if (typeof originalState.shininess === "number") {
              material.shininess = originalState.shininess;
            }
            break;
        }
      }

      if (hasPhongReflectivity(material)) {
        switch (surfacePreset) {
          case "metal":
            material.reflectivity = 1;
            break;
          case "smooth":
            material.reflectivity = 0.12;
            break;
          case "original":
          default:
            if (typeof originalState.reflectivity === "number") {
              material.reflectivity = originalState.reflectivity;
            }
            break;
        }
      }

      if (hasPhysicalSurfaceProperties(material)) {
        switch (surfacePreset) {
          case "metal":
            material.clearcoat = 0.65;
            material.clearcoatRoughness = 0.08;
            break;
          case "smooth":
            material.clearcoat = 0.06;
            material.clearcoatRoughness = 0.72;
            break;
          case "original":
          default:
            if (typeof originalState.clearcoat === "number") {
              material.clearcoat = originalState.clearcoat;
            }
            if (typeof originalState.clearcoatRoughness === "number") {
              material.clearcoatRoughness = originalState.clearcoatRoughness;
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
            if (typeof originalState.flatShading === "boolean") {
              material.flatShading = originalState.flatShading;
            }
            break;
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

const applyShadowPreferences = (
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
  const floorThreshold = bounds.min.y + Math.max(height * 0.02, 0.0005);

  const vertex = new THREE.Vector3();
  let sampledVertices = 0;
  let supportVertices = 0;

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
      }
    }
  });

  if (sampledVertices === 0) {
    return 0;
  }

  return supportVertices / sampledVertices;
};

const autoOrientModelUpright = (object: THREE.Object3D): void => {
  orientLongestAxisUp(object);

  const baseQuaternion = object.quaternion.clone();
  const baseSupportScore = calculateSupportScore(object);

  const flippedQuaternion = baseQuaternion.clone().multiply(
    new THREE.Quaternion().setFromEuler(new THREE.Euler(Math.PI, 0, 0)),
  );

  object.quaternion.copy(flippedQuaternion);
  object.updateMatrixWorld(true);
  const flippedSupportScore = calculateSupportScore(object);

  if (flippedSupportScore <= baseSupportScore) {
    object.quaternion.copy(baseQuaternion);
    object.updateMatrixWorld(true);
  }
};

const applyFlipOrientation = (
  object: THREE.Object3D,
  baseQuaternion: THREE.Quaternion,
  orientationVariant: FlipOrientationVariant,
): void => {
  const variantQuaternion = new THREE.Quaternion().setFromEuler(
    new THREE.Euler(orientationVariant.x, 0, orientationVariant.z),
  );

  object.quaternion.copy(baseQuaternion).multiply(variantQuaternion);

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

const alignModelToGround = (object: THREE.Object3D): void => {
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

const buildGridMetrics = (
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

const prepareModelScene = async (
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

export const ModelPreview: React.FC<ModelPreviewProps> = ({
  source,
  fileName,
  contentType,
  fileSizeBytes,
  materialColor,
  autoAlignToken,
  autoOrientToken,
  flipToken,
  lightingPreset = "balanced",
  shadowsEnabled = true,
  surfacePreset = "metal",
}) => {
  const { t } = useTranslation(["files"]);
  const theme = useTheme();

  const modelFormat = React.useMemo(
    () => resolveModelFormat(fileName, contentType),
    [contentType, fileName],
  );
  const qualityMode = React.useMemo(
    () => resolveQualityMode(fileSizeBytes),
    [fileSizeBytes],
  );
  const lightingConfig = React.useMemo(
    () => LIGHTING_PRESET_CONFIG[lightingPreset],
    [lightingPreset],
  );
  const sceneBackgroundColor = React.useMemo(() => {
    switch (lightingPreset) {
      case "studio":
        return theme.palette.background.paper;
      case "dramatic":
        return theme.palette.grey[900];
      case "balanced":
      default:
        return theme.palette.background.default;
    }
  }, [lightingPreset, theme]);
  const environmentPreset = lightingPreset === "dramatic"
    ? "city"
    : "studio";
  const defaultDarkModelColor = React.useMemo<string | null>(() => {
    return theme.palette.mode === "dark"
      ? theme.palette.grey[700]
      : null;
  }, [theme]);
  const gridLineColor = React.useMemo(() => {
    return theme.palette.mode === "dark"
      ? theme.palette.grey[700]
      : theme.palette.grey[400];
  }, [theme]);
  const gridSubLineColor = React.useMemo(() => {
    return theme.palette.mode === "dark"
      ? theme.palette.grey[800]
      : theme.palette.grey[200];
  }, [theme]);
  const effectiveMaterialColor = React.useMemo<string | null | undefined>(() => {
    return materialColor === undefined
      ? defaultDarkModelColor
      : materialColor;
  }, [defaultDarkModelColor, materialColor]);
  const lightIntensityMultiplier = effectiveMaterialColor
      ? 0.68
      : 1;

  const sourceKey = source.kind === "fileId"
    ? `file:${source.fileId}`
    : `url:${source.url}`;

  const [isLoading, setIsLoading] = React.useState<boolean>(Boolean(modelFormat));
  const [hasLoadError, setHasLoadError] = React.useState<boolean>(false);
  const [preparedModel, setPreparedModel] = React.useState<PreparedModelScene | null>(null);
  const previousAutoAlignTokenRef = React.useRef<number | undefined>(
    autoAlignToken,
  );
  const previousAutoOrientTokenRef = React.useRef<number | undefined>(
    autoOrientToken,
  );
  const previousFlipTokenRef = React.useRef<number | undefined>(
    flipToken,
  );
  const originalColorsRef = React.useRef<WeakMap<THREE.Material, THREE.Color>>(
    new WeakMap(),
  );
  const originalSurfaceRef = React.useRef<WeakMap<THREE.Material, MaterialSurfaceState>>(
    new WeakMap(),
  );
  const flipBaseQuaternionRef = React.useRef<THREE.Quaternion | null>(null);
  const flipOrientationIndexRef = React.useRef<number>(0);

  React.useEffect(() => {
    if (!modelFormat) {
      setIsLoading(false);
      setHasLoadError(false);
      setPreparedModel(null);
      originalColorsRef.current = new WeakMap();
      originalSurfaceRef.current = new WeakMap();
      flipBaseQuaternionRef.current = null;
      flipOrientationIndexRef.current = 0;
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setHasLoadError(false);
    originalColorsRef.current = new WeakMap();
    originalSurfaceRef.current = new WeakMap();
    flipBaseQuaternionRef.current = null;
    flipOrientationIndexRef.current = 0;

    void (async () => {
      try {
        const url = await resolveSourceUrlByKey(sourceKey);
        const loadedObject = await loadModelObject(modelFormat, url);
        const nextPreparedModel = await prepareModelScene(
          loadedObject,
          qualityMode,
        );

        if (cancelled) {
          disposeObject3D(loadedObject);
          return;
        }

        flipBaseQuaternionRef.current = nextPreparedModel.object.quaternion.clone();
        flipOrientationIndexRef.current = 0;
        setPreparedModel(nextPreparedModel);
        setHasLoadError(false);
      } catch {
        if (!cancelled) {
          setPreparedModel(null);
          setHasLoadError(true);
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [modelFormat, qualityMode, sourceKey]);

  React.useEffect(() => {
    if (!preparedModel) {
      return;
    }

    applyMaterialColor(
      preparedModel.object,
      effectiveMaterialColor,
      originalColorsRef.current,
    );
  }, [effectiveMaterialColor, preparedModel]);

  React.useEffect(() => {
    if (!preparedModel) {
      return;
    }

    applyMaterialSurfacePreset(
      preparedModel.object,
      surfacePreset,
      originalSurfaceRef.current,
    );
    applyShadowPreferences(preparedModel.object, shadowsEnabled);
  }, [preparedModel, shadowsEnabled, surfacePreset]);

  React.useEffect(() => {
    if (autoAlignToken === undefined) {
      return;
    }

    if (autoAlignToken === previousAutoAlignTokenRef.current) {
      return;
    }

    previousAutoAlignTokenRef.current = autoAlignToken;

    setPreparedModel((previous) => {
      if (!previous) {
        return previous;
      }

      alignModelToGround(previous.object);
      const metrics = buildGridMetrics(previous.object, previous.qualityMode);

      return {
        ...previous,
        gridSize: metrics.gridSize,
        gridDivisions: metrics.gridDivisions,
      };
    });
  }, [autoAlignToken]);

  React.useEffect(() => {
    if (autoOrientToken === undefined) {
      return;
    }

    if (autoOrientToken === previousAutoOrientTokenRef.current) {
      return;
    }

    previousAutoOrientTokenRef.current = autoOrientToken;

    setPreparedModel((previous) => {
      if (!previous) {
        return previous;
      }

      autoOrientModelUpright(previous.object);
      alignModelToGround(previous.object);
      const metrics = buildGridMetrics(previous.object, previous.qualityMode);
      flipBaseQuaternionRef.current = previous.object.quaternion.clone();
      flipOrientationIndexRef.current = 0;

      return {
        ...previous,
        gridSize: metrics.gridSize,
        gridDivisions: metrics.gridDivisions,
      };
    });
  }, [autoOrientToken]);

  React.useEffect(() => {
    if (flipToken === undefined) {
      return;
    }

    if (flipToken === previousFlipTokenRef.current) {
      return;
    }

    previousFlipTokenRef.current = flipToken;

    if (!preparedModel) {
      return;
    }

    if (!flipBaseQuaternionRef.current) {
      flipBaseQuaternionRef.current = preparedModel.object.quaternion.clone();
      flipOrientationIndexRef.current = 0;
    }

    const nextOrientationIndex =
      (flipOrientationIndexRef.current + 1) % FLIP_ORIENTATION_VARIANTS.length;
    flipOrientationIndexRef.current = nextOrientationIndex;

    applyFlipOrientation(
      preparedModel.object,
      flipBaseQuaternionRef.current,
      FLIP_ORIENTATION_VARIANTS[nextOrientationIndex],
    );
    alignModelToGround(preparedModel.object);
  }, [flipToken, preparedModel]);

  React.useEffect(() => {
    return () => {
      if (preparedModel) {
        disposeObject3D(preparedModel.object);
      }
    };
  }, [preparedModel]);

  if (!modelFormat) {
    return (
      <Box
        sx={{
          width: "100%",
          height: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          p: 2,
        }}
      >
        <Typography color="text.secondary">
          {t("preview.errors.modelUnsupportedType", { ns: "files" })}
        </Typography>
      </Box>
    );
  }

  return (
    <Box
      sx={{
        width: "100%",
        height: "100%",
        minHeight: 0,
        position: "relative",
      }}
    >
      {(isLoading || hasLoadError || !preparedModel) && (
        <Box
          sx={{
            position: "absolute",
            inset: 0,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 1,
            px: 2,
          }}
        >
          {isLoading && (
            <Box
              sx={{
                display: "flex",
                alignItems: "center",
                gap: 1,
              }}
            >
              <CircularProgress size={20} />
              <Typography color="text.secondary">
                {t("preview.model.loading", { ns: "files" })}
              </Typography>
            </Box>
          )}

          {!isLoading && hasLoadError && (
            <Typography color="error">
              {t("preview.errors.modelLoadFailed", { ns: "files" })}
            </Typography>
          )}
        </Box>
      )}

      {!hasLoadError && preparedModel && (
        <Canvas
          shadows={shadowsEnabled ? { type: THREE.PCFShadowMap } : false}
          camera={{
            position: [2.5, 2.5, 2.5],
            fov: 45,
            near: 0.01,
            far: 1000,
          }}
          dpr={
            preparedModel.qualityMode === "reduced"
              ? [1, 1]
              : [1, 2]
          }
        >
          <color attach="background" args={[sceneBackgroundColor]} />
          <Environment preset={environmentPreset} />
          <hemisphereLight
            args={[
              theme.palette.common.white,
              theme.palette.grey[900],
              lightingConfig.ambientIntensity * lightIntensityMultiplier,
            ]}
          />
          <directionalLight
            position={[9, 11, 8]}
            intensity={lightingConfig.keyIntensity * lightIntensityMultiplier}
            castShadow={shadowsEnabled}
          />
          <directionalLight
            position={[-7, 5, -6]}
            intensity={lightingConfig.fillIntensity * lightIntensityMultiplier}
            castShadow={false}
          />
          <directionalLight
            position={[0, 7, -10]}
            intensity={lightingConfig.rimIntensity * lightIntensityMultiplier}
            castShadow={false}
          />

          {shadowsEnabled && (
            <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, -0.012, 0]} receiveShadow>
              <planeGeometry args={[preparedModel.gridSize, preparedModel.gridSize]} />
              <shadowMaterial transparent opacity={0.28} />
            </mesh>
          )}

          <gridHelper
            args={[
              preparedModel.gridSize,
              preparedModel.gridDivisions,
              gridLineColor,
              gridSubLineColor,
            ]}
            position={[0, -0.01, 0]}
          />

          <Bounds fit clip margin={1.2}>
            <primitive object={preparedModel.object} />
          </Bounds>

          <OrbitControls makeDefault enableDamping dampingFactor={0.08} />
        </Canvas>
      )}
    </Box>
  );
};
