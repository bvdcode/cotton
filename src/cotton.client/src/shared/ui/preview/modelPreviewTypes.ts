import * as THREE from "three";

export type ModelPreviewSource =
  | {
      kind: "fileId";
      fileId: string;
    }
  | {
      kind: "url";
      url: string;
    };

export type PreviewQualityMode = "normal" | "reduced";
export type ModelLightingPreset = "balanced" | "studio" | "dramatic";
export type ModelSurfacePreset = "original" | "metal" | "smooth";

export interface ModelPreviewProps {
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

export interface PreparedModelScene {
  object: THREE.Object3D;
  gridSize: number;
  gridDivisions: number;
  qualityMode: PreviewQualityMode;
}

export interface LightingPresetConfig {
  ambientIntensity: number;
  keyIntensity: number;
  fillIntensity: number;
  rimIntensity: number;
}

export interface FlipOrientationVariant {
  quaternion: THREE.Quaternion;
}

export interface MaterialSurfaceState {
  metalness?: number;
  roughness?: number;
  envMapIntensity?: number;
  shininess?: number;
  reflectivity?: number;
  clearcoat?: number;
  clearcoatRoughness?: number;
  flatShading?: boolean;
}

export type MeshStoredMaterial = THREE.Material | THREE.Material[];