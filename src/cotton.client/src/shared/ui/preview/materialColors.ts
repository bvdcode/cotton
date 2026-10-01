import * as THREE from "three";

const hasColorProperty = (
  material: THREE.Material,
): material is THREE.Material & { color: THREE.Color } => {
  const materialWithColor = material as THREE.Material & {
    color?: THREE.Color;
  };
  return "color" in material && materialWithColor.color instanceof THREE.Color;
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
