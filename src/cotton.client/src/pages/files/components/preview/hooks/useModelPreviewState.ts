import * as React from "react";
import * as THREE from "three";
import { type ModelFormat } from "../../../utils/modelFormats";
import {
  alignModelToGround,
  applyFlipOrientation,
  applyMaterialColor,
  applyMaterialSurfacePreset,
  applyPreviewOverrideMaterials,
  applyShadowPreferences,
  autoOrientModelUpright,
  buildGridMetrics,
  disposeObject3D,
  loadPreparedModelScene,
  MANUAL_FLIP_ORIENTATION_VARIANTS,
} from "../modelPreviewCore";
import {
  type MaterialSurfaceState,
  type MeshStoredMaterial,
  type ModelSurfacePreset,
  type PreparedModelScene,
  type PreviewQualityMode,
} from "../modelPreviewTypes";

interface UseModelPreviewStateParams {
  autoAlignToken?: number;
  autoOrientToken?: number;
  flipToken?: number;
  modelFormat: ModelFormat | null;
  qualityMode: PreviewQualityMode;
  sourceKey: string;
  effectiveMaterialColor?: string | null;
  hasColorOverride: boolean;
  shadowsEnabled: boolean;
  surfacePreset: ModelSurfacePreset;
}

interface UseModelPreviewStateResult {
  hasLoadError: boolean;
  isLoading: boolean;
  preparedModel: PreparedModelScene | null;
}

export const useModelPreviewState = ({
  autoAlignToken,
  autoOrientToken,
  flipToken,
  modelFormat,
  qualityMode,
  sourceKey,
  effectiveMaterialColor,
  hasColorOverride,
  shadowsEnabled,
  surfacePreset,
}: UseModelPreviewStateParams): UseModelPreviewStateResult => {
  const [isLoading, setIsLoading] = React.useState<boolean>(Boolean(modelFormat));
  const [hasLoadError, setHasLoadError] = React.useState<boolean>(false);
  const [preparedModel, setPreparedModel] = React.useState<PreparedModelScene | null>(null);

  const previousAutoAlignTokenRef = React.useRef<number | undefined>(
    autoAlignToken,
  );
  const previousAutoOrientTokenRef = React.useRef<number | undefined>(
    autoOrientToken,
  );
  const previousFlipTokenRef = React.useRef<number | undefined>(flipToken);
  const originalColorsRef = React.useRef<WeakMap<THREE.Material, THREE.Color>>(
    new WeakMap(),
  );
  const originalSurfaceRef = React.useRef<
    WeakMap<THREE.Material, MaterialSurfaceState>
  >(new WeakMap());
  const originalMeshMaterialsRef = React.useRef<
    WeakMap<THREE.Mesh, MeshStoredMaterial>
  >(new WeakMap());
  const flipBaseQuaternionRef = React.useRef<THREE.Quaternion | null>(null);
  const flipOrientationIndexRef = React.useRef<number>(0);

  const resetRuntimeState = React.useCallback((): void => {
    originalColorsRef.current = new WeakMap();
    originalSurfaceRef.current = new WeakMap();
    originalMeshMaterialsRef.current = new WeakMap();
    flipBaseQuaternionRef.current = null;
    flipOrientationIndexRef.current = 0;
  }, []);

  React.useEffect(() => {
    if (!modelFormat) {
      setIsLoading(false);
      setHasLoadError(false);
      setPreparedModel(null);
      resetRuntimeState();
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setHasLoadError(false);
    resetRuntimeState();

    void (async () => {
      try {
        const nextPreparedModel = await loadPreparedModelScene(
          modelFormat,
          sourceKey,
          qualityMode,
        );

        if (cancelled) {
          disposeObject3D(nextPreparedModel.object);
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
  }, [modelFormat, qualityMode, resetRuntimeState, sourceKey]);

  React.useEffect(() => {
    if (!preparedModel) {
      return;
    }

    applyPreviewOverrideMaterials(
      preparedModel.object,
      hasColorOverride,
      originalMeshMaterialsRef.current,
    );
    applyMaterialSurfacePreset(
      preparedModel.object,
      surfacePreset,
      originalSurfaceRef.current,
      hasColorOverride,
    );
    applyShadowPreferences(preparedModel.object, shadowsEnabled);
    applyMaterialColor(
      preparedModel.object,
      effectiveMaterialColor,
      originalColorsRef.current,
    );
  }, [
    effectiveMaterialColor,
    hasColorOverride,
    preparedModel,
    shadowsEnabled,
    surfacePreset,
  ]);

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
      (flipOrientationIndexRef.current + 1) % MANUAL_FLIP_ORIENTATION_VARIANTS.length;
    flipOrientationIndexRef.current = nextOrientationIndex;

    applyFlipOrientation(
      preparedModel.object,
      flipBaseQuaternionRef.current,
      MANUAL_FLIP_ORIENTATION_VARIANTS[nextOrientationIndex],
    );
    alignModelToGround(preparedModel.object);
  }, [flipToken, preparedModel]);

  React.useEffect(() => {
    const preparedObject = preparedModel?.object;

    return () => {
      if (preparedObject) {
        applyPreviewOverrideMaterials(
          preparedObject,
          false,
          originalMeshMaterialsRef.current,
        );
        disposeObject3D(preparedObject);
      }
    };
  }, [preparedModel?.object]);

  return {
    hasLoadError,
    isLoading,
    preparedModel,
  };
};