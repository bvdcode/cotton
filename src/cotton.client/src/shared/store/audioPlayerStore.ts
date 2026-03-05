import { create } from "zustand";
import { nodesApi } from "../api/nodesApi";
import type { AudioPlaylistItem } from "../types/audio";
import { getFileTypeInfo } from "../../pages/files/utils/fileTypes";

interface AudioPlayerState {
  open: boolean;
  isScanning: boolean;

  scanRootNodeId: string | null;

  playlist: ReadonlyArray<AudioPlaylistItem>;
  currentFileId: string | null;
  currentFileName: string | null;

  openFromSelection: (args: {
    fileId: string;
    fileName: string;
    playlist?: ReadonlyArray<AudioPlaylistItem> | null;
  }) => void;

  setCurrentTrack: (item: AudioPlaylistItem) => void;

  setScanRootNodeId: (nodeId: string) => void;
  scanRecursively: () => Promise<void>;

  close: () => void;
  reset: () => void;
}

const SCAN_PAGE_SIZE = 500;
const MAX_FOLDERS_TO_SCAN = 2500;
const MAX_AUDIO_FILES = 25000;

const fetchAllChildren = async (nodeId: string): Promise<{
  nodes: Array<{ id: string; name: string }>;
  files: Array<{ id: string; name: string; contentType: string }>;
}> => {
  const nodes: Array<{ id: string; name: string }> = [];
  const files: Array<{ id: string; name: string; contentType: string }> = [];

  let page = 1;
  let total = 0;

  while (true) {
    const response = await nodesApi.getChildren(nodeId, {
      page,
      pageSize: SCAN_PAGE_SIZE,
      depth: 0,
    });

    total = response.totalCount;

    for (const n of response.content.nodes) {
      nodes.push({ id: n.id, name: n.name });
    }

    for (const f of response.content.files) {
      files.push({
        id: f.id,
        name: f.name,
        contentType: f.contentType,
      });
    }

    const fetched = nodes.length + files.length;
    if (fetched >= total) {
      break;
    }

    page += 1;
    // Safety valve in case server misreports totals.
    if (page > 2000) {
      break;
    }
  }

  return { nodes, files };
};

const buildRecursiveAudioPlaylist = async (
  rootNodeId: string,
): Promise<AudioPlaylistItem[]> => {
  const visited = new Set<string>();
  const stack: string[] = [rootNodeId];

  const playlist: AudioPlaylistItem[] = [];

  while (stack.length > 0) {
    if (visited.size >= MAX_FOLDERS_TO_SCAN) {
      break;
    }
    if (playlist.length >= MAX_AUDIO_FILES) {
      break;
    }

    const nodeId = stack.pop();
    if (!nodeId) {
      continue;
    }

    if (visited.has(nodeId)) {
      continue;
    }
    visited.add(nodeId);

    const children = await fetchAllChildren(nodeId);

    const sortedNodes = children.nodes.slice();
    sortedNodes.sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }));

    for (let i = sortedNodes.length - 1; i >= 0; i -= 1) {
      stack.push(sortedNodes[i].id);
    }

    const audioFiles = children.files
      .filter((file) => getFileTypeInfo(file.name, file.contentType).type === "audio")
      .slice();

    audioFiles.sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }));

    for (const file of audioFiles) {
      if (playlist.length >= MAX_AUDIO_FILES) {
        break;
      }
      playlist.push({ id: file.id, name: file.name });
    }
  }

  return playlist;
};

export const useAudioPlayerStore = create<AudioPlayerState>()((set, get) => ({
  open: false,
  isScanning: false,
  scanRootNodeId: null,
  playlist: [],
  currentFileId: null,
  currentFileName: null,

  openFromSelection: ({ fileId, fileName, playlist }) => {
    const effectivePlaylist = (playlist ?? []).length
      ? (playlist ?? [])
      : [{ id: fileId, name: fileName }];

    set({
      open: true,
      currentFileId: fileId,
      currentFileName: fileName,
      playlist: effectivePlaylist,
    });
  },

  setCurrentTrack: (item) => {
    const currentId = get().currentFileId;
    const currentName = get().currentFileName;
    if (currentId === item.id && currentName === item.name) {
      return;
    }

    set({ currentFileId: item.id, currentFileName: item.name });
  },

  setScanRootNodeId: (nodeId) => {
    set({ scanRootNodeId: nodeId });
  },

  scanRecursively: async () => {
    const rootNodeId = get().scanRootNodeId;
    if (!rootNodeId) {
      return;
    }

    if (get().isScanning) {
      return;
    }

    set({ isScanning: true });

    try {
      const next = await buildRecursiveAudioPlaylist(rootNodeId);

      const currentId = get().currentFileId;
      const currentName = get().currentFileName;

      if (currentId && currentName) {
        if (!next.some((x) => x.id === currentId)) {
          next.unshift({ id: currentId, name: currentName });
        }
      }

      set({ playlist: next });
    } finally {
      set({ isScanning: false });
    }
  },

  close: () => set({ open: false }),

  reset: () =>
    set({
      open: false,
      isScanning: false,
      scanRootNodeId: null,
      playlist: [],
      currentFileId: null,
      currentFileName: null,
    }),
}));

export const selectAudioPlayerOpen = (s: AudioPlayerState): boolean => s.open;
export const selectAudioPlayerIsScanning = (s: AudioPlayerState): boolean => s.isScanning;
export const selectAudioPlayerPlaylist = (s: AudioPlayerState): ReadonlyArray<AudioPlaylistItem> => s.playlist;
export const selectAudioPlayerCurrentFileId = (s: AudioPlayerState): string | null => s.currentFileId;
export const selectAudioPlayerCurrentFileName = (s: AudioPlayerState): string | null => s.currentFileName;
