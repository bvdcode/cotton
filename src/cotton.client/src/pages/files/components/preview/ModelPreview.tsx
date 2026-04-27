import * as React from "react";
import { Box, CircularProgress, Typography } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import { Canvas } from "@react-three/fiber";
import { Bounds, OrbitControls } from "@react-three/drei";
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

interface ModelPreviewProps {
  source: ModelPreviewSource;
  fileName: string;
  contentType?: string | null;
  fileSizeBytes?: number | null;
}

interface PreparedModelScene {
  object: THREE.Object3D;
  gridSize: number;
  gridDivisions: number;
  qualityMode: PreviewQualityMode;
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

const resolveSourceUrl = async (source: ModelPreviewSource): Promise<string> => {
  if (source.kind === "url") {
    return toAbsoluteUrl(source.url);
  }

  const downloadUrl = await filesApi.getDownloadLink(
    source.fileId,
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

  const [isLoading, setIsLoading] = React.useState<boolean>(Boolean(modelFormat));
  const [hasLoadError, setHasLoadError] = React.useState<boolean>(false);
  const [preparedModel, setPreparedModel] = React.useState<PreparedModelScene | null>(null);

  React.useEffect(() => {
    if (!modelFormat) {
      setIsLoading(false);
      setHasLoadError(false);
      setPreparedModel(null);
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setHasLoadError(false);

    void (async () => {
      try {
        const url = await resolveSourceUrl(source);
        const loadedObject = await loadModelObject(modelFormat, url);
        const nextPreparedModel = await prepareModelScene(
          loadedObject,
          qualityMode,
        );

        if (cancelled) {
          disposeObject3D(loadedObject);
          return;
        }

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
  }, [modelFormat, qualityMode, source]);

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
          <color attach="background" args={[theme.palette.background.default]} />
          <ambientLight intensity={0.75} />
          <directionalLight position={[8, 8, 8]} intensity={0.8} />
          <directionalLight position={[-6, 4, -6]} intensity={0.45} />

          <gridHelper
            args={[
              preparedModel.gridSize,
              preparedModel.gridDivisions,
              theme.palette.divider,
              theme.palette.action.disabled,
            ]}
            position={[0, -0.01, 0]}
          />

          <Bounds fit clip observe margin={1.2}>
            <primitive object={preparedModel.object} />
          </Bounds>

          <OrbitControls makeDefault enableDamping dampingFactor={0.08} />
        </Canvas>
      )}
    </Box>
  );
};
