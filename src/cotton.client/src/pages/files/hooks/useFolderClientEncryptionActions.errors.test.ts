import { act, cleanup, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { useVault } from "../../../shared/crypto";
import { useFolderClientEncryptionActions } from "./useFolderClientEncryptionActions";
import { fetchServerSettings } from "../../../shared/api/queries/serverSettings";
import { refreshNodeContent } from "../../../shared/store/nodesActions";
import {
  collectPlainFilesInFoldersForClientEncryption,
  collectEncryptedFilesInFoldersForClientEncryption,
} from "../../../shared/utils/clientEncryptionFolderScan";
import {
  encryptExistingFileWithTask,
  decryptExistingFileWithTask,
} from "../../../shared/tasks";

vi.mock("../../../shared/api/queries/serverSettings", () => ({
  fetchServerSettings: vi.fn(),
}));
vi.mock("../../../shared/store/nodesActions", () => ({
  refreshNodeContent: vi.fn(),
}));
vi.mock("../../../shared/utils/clientEncryptionFolderScan", () => ({
  collectPlainFilesInFoldersForClientEncryption: vi.fn(),
  collectEncryptedFilesInFoldersForClientEncryption: vi.fn(),
}));
vi.mock("../../../shared/tasks", () => ({
  encryptExistingFileWithTask: vi.fn(),
  decryptExistingFileWithTask: vi.fn(),
  taskManager: {
    createTask: () => ({
      id: "task",
      update: vi.fn(),
      complete: vi.fn(),
      fail: vi.fn(),
    }),
  },
}));

const cases = [
  {
    action: "encryptPlainFiles",
    busy: "isEncryptingPlainFiles",
    scan: collectPlainFilesInFoldersForClientEncryption,
    execute: encryptExistingFileWithTask,
    prefix: "encryptExisting",
  },
  {
    action: "decryptEncryptedFiles",
    busy: "isDecryptingEncryptedFiles",
    scan: collectEncryptedFilesInFoldersForClientEncryption,
    execute: decryptExistingFileWithTask,
    prefix: "decryptExisting",
  },
] as const;

beforeEach(() => {
  vi.resetAllMocks();
  useVault.setState({ isUnlocked: true });
  vi.mocked(fetchServerSettings).mockResolvedValue({
    version: "1",
    maxChunkSizeBytes: 1024,
    supportedHashAlgorithm: "SHA-256",
  });
});
afterEach(() => {
  cleanup();
  useVault.getState().lock();
});

describe.each(cases)(
  "$action errors",
  ({ action, busy, scan, execute, prefix }) => {
    const setup = () => {
      const onToast = vi.fn();
      const node = createFolder({ id: "root" });
      const file = createFile({ nodeId: "root" });
      vi.mocked(scan).mockResolvedValue({
        files: [file],
        folders: [],
        scannedFolders: 1,
        truncated: false,
      });
      const hook = renderHook(() =>
        useFolderClientEncryptionActions({
          nodeId: node.id,
          currentNode: node,
          content: { ...node, files: [file], nodes: [] },
          onToast,
        }),
      );
      return { ...hook, onToast, file };
    };

    it("reports a settings failure without scanning or modifying files", async () => {
      const { result, onToast } = setup();
      vi.mocked(fetchServerSettings).mockRejectedValue(
        new Error("settings unavailable"),
      );
      await act(async () => {
        await result.current[action]();
      });
      expect(scan).not.toHaveBeenCalled();
      expect(execute).not.toHaveBeenCalled();
      expect(onToast).toHaveBeenCalledExactlyOnceWith(
        "errors.serverSettingsNotLoaded",
        "error",
      );
      expect(result.current[busy]).toBe(false);
    });

    it("stops after a scan failure instead of processing stale visible files", async () => {
      const { result, onToast } = setup();
      vi.mocked(scan).mockRejectedValue(new Error("scan unavailable"));
      await act(async () => {
        await result.current[action]();
      });
      expect(execute).not.toHaveBeenCalled();
      expect(onToast).toHaveBeenCalledExactlyOnceWith(
        `clientEncryption.toasts.${prefix}ScanIncomplete`,
        "error",
      );
      expect(result.current[busy]).toBe(false);
    });

    it("honors an empty scan instead of processing stale visible files", async () => {
      const { result, onToast } = setup();
      vi.mocked(scan).mockResolvedValue({
        files: [],
        folders: [],
        scannedFolders: 1,
        truncated: false,
      });
      await act(async () => {
        await result.current[action]();
      });
      expect(execute).not.toHaveBeenCalled();
      expect(onToast).not.toHaveBeenCalled();
    });

    it("continues after a file failure and reports both outcomes", async () => {
      const { result, onToast, file } = setup();
      vi.mocked(scan).mockResolvedValue({
        files: [file, createFile({ id: "second", nodeId: "child" })],
        folders: [],
        scannedFolders: 2,
        truncated: false,
      });
      vi.mocked(execute)
        .mockRejectedValueOnce(new Error("file failed"))
        .mockResolvedValueOnce();
      await act(async () => {
        await result.current[action]();
      });
      expect(execute).toHaveBeenCalledTimes(2);
      expect(onToast).toHaveBeenCalledWith(
        `clientEncryption.toasts.${prefix}Failed`,
        "error",
      );
      expect(onToast).toHaveBeenCalledWith(
        `clientEncryption.toasts.${prefix}Complete`,
      );
      expect(refreshNodeContent).toHaveBeenCalledWith("child");
      expect(result.current[busy]).toBe(false);
    });

    it("reports a refresh failure separately from successful file changes", async () => {
      const { result, onToast } = setup();
      vi.mocked(refreshNodeContent).mockRejectedValue(
        new Error("refresh failed"),
      );
      await act(async () => {
        await result.current[action]();
      });
      expect(onToast).toHaveBeenCalledWith(
        `clientEncryption.toasts.${prefix}Complete`,
      );
      expect(onToast).toHaveBeenCalledWith(
        "errors.loadContentsFailed",
        "error",
      );
      expect(onToast).not.toHaveBeenCalledWith(
        "errors.serverSettingsNotLoaded",
        "error",
      );
      expect(result.current[busy]).toBe(false);
    });
  },
);
