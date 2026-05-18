import type {
  LightingPresetConfig,
  ModelLightingPreset,
} from "./modelPreviewTypes";

/**
 * Intensity profiles for the three lighting presets exposed in the model
 * preview toolbar. Picked by ModelPreviewScene to drive its three-point rig
 * (ambient + key + fill + rim).
 */
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
