import * as React from "react";
import * as THREE from "three";
import { type ModelFormat } from "@shared/utils/modelFormats";
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

type ModelLoadState = {
  key: string;
  hasLoadError: boolean;
  isLoading: boolean;
  preparedModel: PreparedModelScene | null;
};

const createIdleModelLoadState = (key: string): ModelLoadState => ({
  key,
  hasLoadError: false,
  isLoading: false,
  preparedModel: null,
});

const createLoadingModelLoadState = (key: string): ModelLoadState => ({
  key,
  hasLoadError: false,
  isLoading: true,
  preparedModel: null,
});

const buildModelLoadKey = (
  modelFormat: ModelFormat | null,
  sourceKey: string,
  qualityMode: PreviewQualityMode,
): string => {
  return modelFormat
    ? [modelFormat, sourceKey, qualityMode].join("\u0000")
    : "";
};

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
  const modelLoadKey = buildModelLoadKey(modelFormat, sourceKey, qualityMode);
  const [modelLoadState, setModelLoadState] = React.useState<ModelLoadState>(
    () =>
      modelFormat
        ? createLoadingModelLoadState(modelLoadKey)
        : createIdleModelLoadState(modelLoadKey),
  );
  const effectiveModelLoadState = !modelFormat
    ? createIdleModelLoadState(modelLoadKey)
    : modelLoadState.key === modelLoadKey
      ? modelLoadState
      : createLoadingModelLoadState(modelLoadKey);
  const { hasLoadError, isLoading, preparedModel } = effectiveModelLoadState;

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

  const updatePreparedModel = React.useCallback(
    (
      updater: (
        previous: PreparedModelScene | null,
      ) => PreparedModelScene | null,
    ) => {
      setModelLoadState((previousState) => {
        const current =
          previousState.key === modelLoadKey
            ? previousState
            : modelFormat
              ? createLoadingModelLoadState(modelLoadKey)
              : createIdleModelLoadState(modelLoadKey);
        const nextPreparedModel = updater(current.preparedModel);
        if (nextPreparedModel === current.preparedModel) {
          return previousState.key === modelLoadKey ? previousState : current;
        }

        return {
          ...current,
          preparedModel: nextPreparedModel,
        };
      });
    },
    [modelFormat, modelLoadKey],
  );

  React.useEffect(() => {
    if (!modelFormat) {
      resetRuntimeState();
      return;
    }

    let cancelled = false;
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

        flipBaseQuaternionRef.current =
          nextPreparedModel.object.quaternion.clone();
        flipOrientationIndexRef.current = 0;
        setModelLoadState({
          key: modelLoadKey,
          hasLoadError: false,
          isLoading: false,
          preparedModel: nextPreparedModel,
        });
      } catch {
        if (!cancelled) {
          setModelLoadState({
            key: modelLoadKey,
            hasLoadError: true,
            isLoading: false,
            preparedModel: null,
          });
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [modelFormat, modelLoadKey, qualityMode, resetRuntimeState, sourceKey]);

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

    updatePreparedModel((previous) => {
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
  }, [autoAlignToken, updatePreparedModel]);

  React.useEffect(() => {
    if (autoOrientToken === undefined) {
      return;
    }

    if (autoOrientToken === previousAutoOrientTokenRef.current) {
      return;
    }

    previousAutoOrientTokenRef.current = autoOrientToken;

    updatePreparedModel((previous) => {
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
  }, [autoOrientToken, updatePreparedModel]);

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
      (flipOrientationIndexRef.current + 1) %
      MANUAL_FLIP_ORIENTATION_VARIANTS.length;
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
