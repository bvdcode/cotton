import * as THREE from "three";
import type {
  MaterialSurfaceState,
  MeshStoredMaterial,
  ModelSurfacePreset,
} from "./modelPreviewTypes";

/**
 * Material-side concerns for the model preview pipeline: cleanup of GPU
 * resources, the preview-override material (used when the user paints the
 * model a uniform colour) and the metal/smooth/original surface presets.
 */

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

  return (
    typeof candidate.metalness === "number" &&
    typeof candidate.roughness === "number"
  );
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

  return (
    typeof candidate.clearcoat === "number" &&
    typeof candidate.clearcoatRoughness === "number"
  );
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
