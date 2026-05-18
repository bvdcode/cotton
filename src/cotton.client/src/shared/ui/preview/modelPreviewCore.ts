import type * as THREE from "three";
import type { ModelFormat } from "@shared/utils/modelFormats";
import type {
  PreparedModelScene,
  PreviewQualityMode,
} from "./modelPreviewTypes";
import {
  alignModelToGround,
  buildGridMetrics,
  ensureMeshNormals,
  normalizeModelScale,
} from "./orientation";
import {
  loadModelObjectFromSource,
  simplifyObjectGeometry,
} from "./loader";

/**
 * Orchestrator for the model preview pipeline. The implementation pieces
 * live next to this file — see lighting.ts, materials.ts, orientation.ts
 * and loader.ts — and the symbols they expose are re-exported below so
 * existing consumers (ModelPreview, useModelPreviewState) keep their
 * single import surface.
 */

export { LIGHTING_PRESET_CONFIG } from "./lighting";
export {
  applyMaterialColor,
  applyMaterialSurfacePreset,
  applyPreviewOverrideMaterials,
  applyShadowPreferences,
  disposeObject3D,
} from "./materials";
export {
  alignModelToGround,
  applyFlipOrientation,
  autoOrientModelUpright,
  buildGridMetrics,
  MANUAL_FLIP_ORIENTATION_VARIANTS,
} from "./orientation";
export { resolveQualityMode } from "./loader";

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
  const loadedObject = await loadModelObjectFromSource(modelFormat, sourceKey);
  return prepareModelScene(loadedObject, qualityMode);
};
