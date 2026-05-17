import * as THREE from "three";
import type {
  FlipOrientationVariant,
  PreviewQualityMode,
} from "./modelPreviewTypes";

/**
 * Geometry-side concerns for the model preview pipeline: mesh normals,
 * upright auto-orientation, ground alignment, scale normalization and the
 * grid metrics used by ModelPreviewScene to size its helper grid.
 */

const TARGET_MODEL_MAX_DIMENSION = 4;
const MIN_GRID_SIZE = 6;
const MAX_GRID_SIZE = 48;
const GRID_SIZE_MULTIPLIER = 2.4;
const GRID_DENSITY_FACTOR = 4;
const MIN_GRID_DIVISIONS = 20;
const MAX_GRID_DIVISIONS = 120;
const QUARTER_TURN = Math.PI / 2;

const clamp = (value: number, min: number, max: number): number => {
  return Math.min(Math.max(value, min), max);
};

const AUTO_ORIENT_VARIANTS: ReadonlyArray<FlipOrientationVariant> = [
  { quaternion: new THREE.Quaternion() },
  {
    quaternion: new THREE.Quaternion().setFromAxisAngle(
      new THREE.Vector3(1, 0, 0),
      Math.PI,
    ),
  },
  {
    quaternion: new THREE.Quaternion().setFromAxisAngle(
      new THREE.Vector3(0, 0, 1),
      QUARTER_TURN,
    ),
  },
  {
    quaternion: new THREE.Quaternion().setFromAxisAngle(
      new THREE.Vector3(0, 0, 1),
      -QUARTER_TURN,
    ),
  },
  {
    quaternion: new THREE.Quaternion().setFromAxisAngle(
      new THREE.Vector3(1, 0, 0),
      QUARTER_TURN,
    ),
  },
  {
    quaternion: new THREE.Quaternion().setFromAxisAngle(
      new THREE.Vector3(1, 0, 0),
      -QUARTER_TURN,
    ),
  },
];

export const MANUAL_FLIP_ORIENTATION_VARIANTS: ReadonlyArray<FlipOrientationVariant> =
  AUTO_ORIENT_VARIANTS;

export const ensureMeshNormals = (object: THREE.Object3D): void => {
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
  const footprintWidth = Math.max(bounds.max.x - bounds.min.x, 0.0001);
  const footprintDepth = Math.max(bounds.max.z - bounds.min.z, 0.0001);
  const footprintArea = footprintWidth * footprintDepth;
  const footprintDiagonal = Math.hypot(footprintWidth, footprintDepth);
  const floorThreshold = bounds.min.y + Math.max(height * 0.02, 0.0005);
  const boundsCenter = bounds.getCenter(new THREE.Vector3());

  const vertex = new THREE.Vector3();
  let sampledVertices = 0;
  let supportVertices = 0;
  let supportMinX = Number.POSITIVE_INFINITY;
  let supportMaxX = Number.NEGATIVE_INFINITY;
  let supportMinZ = Number.POSITIVE_INFINITY;
  let supportMaxZ = Number.NEGATIVE_INFINITY;
  let supportSumX = 0;
  let supportSumZ = 0;

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
        supportMinX = Math.min(supportMinX, vertex.x);
        supportMaxX = Math.max(supportMaxX, vertex.x);
        supportMinZ = Math.min(supportMinZ, vertex.z);
        supportMaxZ = Math.max(supportMaxZ, vertex.z);
        supportSumX += vertex.x;
        supportSumZ += vertex.z;
      }
    }
  });

  if (sampledVertices === 0 || supportVertices === 0) {
    return 0;
  }

  const supportRatio = supportVertices / sampledVertices;
  const supportWidth = Math.max(supportMaxX - supportMinX, 0);
  const supportDepth = Math.max(supportMaxZ - supportMinZ, 0);
  const supportAreaRatio = clamp(
    (supportWidth * supportDepth) / footprintArea,
    0,
    1,
  );
  const supportCenterOffset = Math.hypot(
    supportSumX / supportVertices - boundsCenter.x,
    supportSumZ / supportVertices - boundsCenter.z,
  );
  const supportCenterScore =
    1 -
    clamp(
      supportCenterOffset / Math.max(footprintDiagonal * 0.5, 0.0001),
      0,
      1,
    );

  return (
    supportRatio * 0.5 + supportAreaRatio * 0.35 + supportCenterScore * 0.15
  );
};

export const autoOrientModelUpright = (object: THREE.Object3D): void => {
  orientLongestAxisUp(object);

  const baseQuaternion = object.quaternion.clone();
  let bestSupportScore = Number.NEGATIVE_INFINITY;
  const bestQuaternion = baseQuaternion.clone();

  for (const orientationVariant of AUTO_ORIENT_VARIANTS) {
    object.quaternion
      .copy(baseQuaternion)
      .multiply(orientationVariant.quaternion);

    object.updateMatrixWorld(true);
    const supportScore = calculateSupportScore(object);
    if (supportScore > bestSupportScore) {
      bestSupportScore = supportScore;
      bestQuaternion.copy(object.quaternion);
    }
  }

  object.quaternion.copy(bestQuaternion);
  object.updateMatrixWorld(true);
};

export const applyFlipOrientation = (
  object: THREE.Object3D,
  baseQuaternion: THREE.Quaternion,
  orientationVariant: FlipOrientationVariant,
): void => {
  object.quaternion
    .copy(baseQuaternion)
    .multiply(orientationVariant.quaternion);
  object.updateMatrixWorld(true);
};

export const normalizeModelScale = (object: THREE.Object3D): void => {
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

export const alignModelToGround = (object: THREE.Object3D): void => {
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

export const buildGridMetrics = (
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
    clamp(gridSize * densityFactor, MIN_GRID_DIVISIONS, MAX_GRID_DIVISIONS),
  );

  return {
    gridSize,
    gridDivisions,
  };
};
