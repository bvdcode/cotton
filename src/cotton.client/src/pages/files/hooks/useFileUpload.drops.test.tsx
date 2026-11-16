import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { FakeDataTransfer, makeDragEvent } from "../../../test/dragEvents";
import type { SiblingNameLookupDto } from "../../../shared/api/nodesApi";
import type { DroppedFile } from "./scanDroppedFiles";
import { useFileUpload } from "./useFileUpload";

const mocks = vi.hoisted(() => ({
  scan: vi.fn(),
  group: vi.fn(),
  lookup: vi.fn(),
  enqueue: vi.fn(),
  enqueueBatch: vi.fn(),
}));

vi.mock("../../../shared/upload/UploadManager", () => ({
  uploadManager: { enqueue: mocks.enqueue, enqueueBatch: mocks.enqueueBatch },
}));
vi.mock("./useUploadEncryptionPolicy", () => ({
  useUploadEncryptionPolicy: () => ({
    decideEncrypt: async () => ({ encrypt: false, vaultLocked: false }),
    isPolicyEnabledForNode: () => false,
  }),
}));
vi.mock("../utils/lookupUploadNames", () => ({
  lookupUploadNames: mocks.lookup,
}));
vi.mock("./scanDroppedFiles", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./scanDroppedFiles")>()),
  getAllFilesFromEntries: mocks.scan,
}));
vi.mock("./DroppedFolderResolver", () => ({
  DroppedFolderResolver: class {
    groupFiles = mocks.group;
    lookupFileNames = mocks.lookup;
    decideEncryption = () => ({ encrypt: false, vaultLocked: false });
  },
}));

const empty: SiblingNameLookupDto = { nodes: [], files: [], takenNameKeys: [] };
const file = new File(["data"], "file.txt");
const dropped = (folder: string): DroppedFile[] => [
  { file, relativePath: `${folder}/file.txt` },
];

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => {
    resolve = done;
  });
  return { promise, resolve };
}

function drop() {
  const transfer = new FakeDataTransfer();
  const entry: FileSystemEntry = {
    isFile: false,
    isDirectory: true,
    name: "Folder",
    fullPath: "/Folder",
    get filesystem(): FileSystem {
      throw new Error("Unused filesystem");
    },
    getParent: () => {},
  };
  const capture = vi.fn(() => entry);
  Array.prototype.push.call(transfer.items, {
    kind: "file",
    type: "",
    getAsFile: () => null,
    getAsString: () => {},
    webkitGetAsEntry: capture,
  });
  return { event: makeDragEvent(transfer), capture };
}

beforeEach(() => {
  mocks.scan.mockResolvedValue({
    files: dropped("Folder"),
    skippedItems: [],
    skippedNotFound: 0,
  });
  mocks.group.mockImplementation(
    async (files: DroppedFile[], progress: (count: number) => void) => {
      progress(files.length);
      return new Map([
        [
          "target",
          { label: "Files / Folder", files: files.map((item) => item.file) },
        ],
      ]);
    },
  );
  mocks.lookup.mockResolvedValue(empty);
});
afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

it("captures a second drop immediately but serializes preparation and progress", async () => {
  const firstGate = deferred<Map<string, { label: string; files: File[] }>>();
  mocks.group.mockReturnValueOnce(firstGate.promise);
  const { result } = renderHook(() => useFileUpload("root", []));
  let first!: Promise<void>;
  act(() => {
    first = result.current.handleDrop(drop().event);
  });
  await waitFor(() => expect(mocks.group).toHaveBeenCalledOnce());

  const next = drop();
  let second!: Promise<void>;
  act(() => {
    second = result.current.handleDrop(next.event);
  });
  expect(next.capture).toHaveBeenCalledOnce();
  next.capture.mockImplementation(() => {
    throw new Error("Drop event expired");
  });
  await act(async () => {
    await Promise.resolve();
  });
  expect(mocks.scan).toHaveBeenCalledOnce();
  expect(result.current.dropPreparation.step).toBe("folders");

  await act(async () => {
    firstGate.resolve(
      new Map([["first", { label: "Files / First", files: [file] }]]),
    );
    await Promise.all([first, second]);
  });
  expect(mocks.enqueueBatch).toHaveBeenCalledTimes(2);
  expect(result.current.dropPreparation.active).toBe(false);
});

it("continues with a queued drop after the preceding preparation fails", async () => {
  mocks.group.mockRejectedValueOnce(new Error("Folder creation failed"));
  const onToast = vi.fn();
  const { result } = renderHook(() => useFileUpload("root", [], { onToast }));
  await act(async () => {
    await Promise.all([
      result.current.handleDrop(drop().event),
      result.current.handleDrop(drop().event),
    ]);
  });
  expect(onToast).toHaveBeenCalledOnce();
  expect(mocks.enqueueBatch).toHaveBeenCalledOnce();
  expect(result.current.dropPreparation.active).toBe(false);
});

it("shows conflict progress until all targets are ready and enqueues them together", async () => {
  const pending = deferred<SiblingNameLookupDto>();
  mocks.group.mockResolvedValue(
    new Map([
      ["first", { label: "Files / First", files: [file] }],
      ["second", { label: "Files / Second", files: [file] }],
    ]),
  );
  mocks.scan.mockResolvedValue({
    files: [...dropped("First"), ...dropped("Second")],
    skippedItems: [],
    skippedNotFound: 0,
  });
  mocks.lookup
    .mockResolvedValueOnce(empty)
    .mockReturnValueOnce(pending.promise);
  const { result } = renderHook(() => useFileUpload("root", []));
  let operation!: Promise<void>;
  act(() => {
    operation = result.current.handleDrop(drop().event);
  });
  await waitFor(() => expect(mocks.lookup).toHaveBeenCalledTimes(2));
  expect(result.current.dropPreparation).toMatchObject({
    active: true,
    step: "conflicts",
    processed: 1,
    filesFound: 2,
  });
  expect(mocks.enqueueBatch).not.toHaveBeenCalled();
  await act(async () => {
    pending.resolve(empty);
    await operation;
  });
  expect(mocks.enqueueBatch).toHaveBeenCalledOnce();
  expect(mocks.enqueueBatch.mock.calls[0][0]).toMatchObject([
    { nodeId: "first" },
    { nodeId: "second" },
  ]);
  expect(result.current.dropPreparation.active).toBe(false);
});
