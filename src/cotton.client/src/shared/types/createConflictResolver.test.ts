import { describe, expect, it, vi } from "vitest";
import { createConflictResolver } from "./createConflictResolver";
import { ConflictAction } from "./nameConflict";

const filePrompt = { newName: "report (1).txt", canOverwrite: true };
const folderPrompt = { newName: "folder (1)", canOverwrite: false };

describe("createConflictResolver", () => {
  it("replaces remaining files without prompting again", async () => {
    const confirm = vi.fn(async () => ConflictAction.OverwriteAll);
    const resolve = createConflictResolver(confirm);

    expect(await resolve(filePrompt)).toBe(ConflictAction.Overwrite);
    expect(await resolve(filePrompt)).toBe(ConflictAction.Overwrite);
    expect(confirm).toHaveBeenCalledOnce();
  });

  it("asks about non-replaceable conflicts and retains the file choice", async () => {
    const confirm = vi
      .fn()
      .mockResolvedValueOnce(ConflictAction.OverwriteAll)
      .mockResolvedValueOnce(ConflictAction.Rename);
    const resolve = createConflictResolver(confirm);

    expect(await resolve(filePrompt)).toBe(ConflictAction.Overwrite);
    expect(await resolve(folderPrompt)).toBe(ConflictAction.Rename);
    expect(await resolve(filePrompt)).toBe(ConflictAction.Overwrite);
    expect(confirm).toHaveBeenCalledTimes(2);
  });

  it("allows skipping all remaining conflicts after replacing files", async () => {
    const confirm = vi
      .fn()
      .mockResolvedValueOnce(ConflictAction.OverwriteAll)
      .mockResolvedValueOnce(ConflictAction.SkipAll);
    const resolve = createConflictResolver(confirm);

    await resolve(filePrompt);
    expect(await resolve(folderPrompt)).toBe(ConflictAction.SkipAll);
    expect(await resolve(filePrompt)).toBe(ConflictAction.SkipAll);
    expect(confirm).toHaveBeenCalledTimes(2);
  });

  it("does not apply replacement to another operation", async () => {
    const confirm = vi.fn(async () => ConflictAction.OverwriteAll);
    await createConflictResolver(confirm)(filePrompt);
    await createConflictResolver(confirm)(filePrompt);

    expect(confirm).toHaveBeenCalledTimes(2);
  });

  it.each([
    ConflictAction.Overwrite,
    ConflictAction.Rename,
    ConflictAction.Skip,
    ConflictAction.Cancel,
  ])("does not remember an individual %s decision", async (action) => {
    const confirm = vi.fn(async () => action);
    const resolve = createConflictResolver(confirm);

    expect(await resolve(filePrompt)).toBe(action);
    expect(await resolve(filePrompt)).toBe(action);
    expect(confirm).toHaveBeenCalledTimes(2);
  });
});
