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

type ModelPreviewSource =
  | {
      kind: "fileId";
      fileId: string;
    }
  | {
      kind: "url";
      url: string;
    };

interface ModelPreviewProps {
  source: ModelPreviewSource;
  fileName: string;
  contentType?: string | null;
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

export const ModelPreview: React.FC<ModelPreviewProps> = ({
  source,
  fileName,
  contentType,
}) => {
  const { t } = useTranslation(["files"]);
  const theme = useTheme();
  const modelFormat = React.useMemo(
    () => resolveModelFormat(fileName, contentType),
    [contentType, fileName],
  );

  const [isLoading, setIsLoading] = React.useState<boolean>(Boolean(modelFormat));
  const [hasLoadError, setHasLoadError] = React.useState<boolean>(false);
  const [modelObject, setModelObject] = React.useState<THREE.Object3D | null>(null);

  React.useEffect(() => {
    if (!modelFormat) {
      setIsLoading(false);
      setHasLoadError(false);
      setModelObject(null);
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setHasLoadError(false);

    void (async () => {
      try {
        const url = await resolveSourceUrl(source);
        const loadedObject = await loadModelObject(modelFormat, url);

        if (cancelled) {
          disposeObject3D(loadedObject);
          return;
        }

        setModelObject(loadedObject);
        setHasLoadError(false);
      } catch {
        if (!cancelled) {
          setModelObject(null);
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
  }, [modelFormat, source]);

  React.useEffect(() => {
    return () => {
      if (modelObject) {
        disposeObject3D(modelObject);
      }
    };
  }, [modelObject]);

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
      {(isLoading || hasLoadError || !modelObject) && (
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

      {!hasLoadError && modelObject && (
        <Canvas
          camera={{
            position: [2.5, 2.5, 2.5],
            fov: 45,
            near: 0.01,
            far: 1000,
          }}
          dpr={[1, 2]}
        >
          <color attach="background" args={[theme.palette.background.default]} />
          <ambientLight intensity={0.75} />
          <directionalLight position={[8, 8, 8]} intensity={0.8} />
          <directionalLight position={[-6, 4, -6]} intensity={0.45} />

          <gridHelper
            args={[
              10,
              20,
              theme.palette.divider,
              theme.palette.action.disabled,
            ]}
            position={[0, -0.01, 0]}
          />

          <Bounds fit clip observe margin={1.2}>
            <primitive object={modelObject} />
          </Bounds>

          <OrbitControls makeDefault enableDamping dampingFactor={0.08} />
        </Canvas>
      )}
    </Box>
  );
};
