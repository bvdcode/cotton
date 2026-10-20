import { afterEach, describe, expect, it, vi } from "vitest";
import {
  BufferAttribute,
  BufferGeometry,
  Group,
  Mesh,
  SkinnedMesh,
} from "three";
import { SimplifyModifier } from "three/examples/jsm/modifiers/SimplifyModifier.js";
import { simplifyObjectGeometry } from "./loader";

const createLargeMesh = (): Mesh => {
  const geometry = new BufferGeometry();
  geometry.setAttribute(
    "position",
    new BufferAttribute(new Float32Array(500_000 * 3), 3),
  );
  return new Mesh(geometry);
};

afterEach(() => vi.restoreAllMocks());

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
    vi.spyOn(SimplifyModifier.prototype, "modify").mockReturnValue(pending);

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
    vi.spyOn(SimplifyModifier.prototype, "modify")
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
    const modify = vi.spyOn(SimplifyModifier.prototype, "modify");

    await simplifyObjectGeometry(mesh, "normal");
    await simplifyObjectGeometry(skinned, "reduced");

    expect(modify).not.toHaveBeenCalled();
  });
});
