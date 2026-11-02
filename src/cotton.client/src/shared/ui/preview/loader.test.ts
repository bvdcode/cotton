import { afterEach, describe, expect, it, vi } from "vitest";
import {
  BufferAttribute,
  BufferGeometry,
  Group,
  Mesh,
  SkinnedMesh,
} from "three";
import { simplifyObjectGeometry } from "./loader";

const { modifyGeometry } = vi.hoisted(() => ({
  modifyGeometry:
    vi.fn<
      (
        geometry: BufferGeometry,
        count: number,
      ) => BufferGeometry | Promise<BufferGeometry>
    >(),
}));

vi.mock("three/examples/jsm/modifiers/SimplifyModifier.js", () => ({
  SimplifyModifier: class {
    modify(
      geometry: BufferGeometry,
      count: number,
    ): BufferGeometry | Promise<BufferGeometry> {
      return modifyGeometry(geometry, count);
    }
  },
}));

const createLargeMesh = (): Mesh => {
  const geometry = new BufferGeometry();
  geometry.setAttribute(
    "position",
    new BufferAttribute(new Float32Array(500_000 * 3), 3),
  );
  return new Mesh(geometry);
};

afterEach(() => {
  vi.restoreAllMocks();
  modifyGeometry.mockReset();
});

describe("simplifyObjectGeometry", () => {
  it("waits for asynchronous simplification before replacing and disposing geometry", async () => {
    const mesh = createLargeMesh();
    const original = mesh.geometry;
    const simplified = new BufferGeometry();
    const dispose = vi.spyOn(original, "dispose");
    let finish: (geometry: BufferGeometry) => void = () => {
      throw new Error("Simplification has not started");
    };
    const pending = new Promise<BufferGeometry>((resolve) => {
      finish = resolve;
    });
    modifyGeometry.mockReturnValue(pending);

    const operation = simplifyObjectGeometry(mesh, "reduced");
    expect(mesh.geometry).toBe(original);
    expect(dispose).not.toHaveBeenCalled();

    finish(simplified);
    await operation;

    expect(mesh.geometry).toBe(simplified);
    expect(dispose).toHaveBeenCalledOnce();
  });

  it("preserves a failed mesh and continues simplifying the rest", async () => {
    const first = createLargeMesh();
    const second = createLargeMesh();
    const original = first.geometry;
    const simplified = new BufferGeometry();
    const root = new Group();
    root.add(first, second);
    const dispose = vi.spyOn(original, "dispose");
    modifyGeometry
      .mockRejectedValueOnce(new Error("Invalid mesh"))
      .mockResolvedValueOnce(simplified);

    await simplifyObjectGeometry(root, "reduced");

    expect(first.geometry).toBe(original);
    expect(dispose).not.toHaveBeenCalled();
    expect(second.geometry).toBe(simplified);
  });

  it("leaves normal-quality and skinned meshes unchanged", async () => {
    const mesh = createLargeMesh();
    const skinned = new SkinnedMesh(mesh.geometry);

    await simplifyObjectGeometry(mesh, "normal");
    await simplifyObjectGeometry(skinned, "reduced");

    expect(modifyGeometry).not.toHaveBeenCalled();
  });
});
