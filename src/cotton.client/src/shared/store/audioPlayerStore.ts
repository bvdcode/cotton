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
const MAX_SCAN_DEPTH = 256;
const MAX_FOLDERS_TO_SCAN = 2500;
const MAX_AUDIO_FILES = 25000;

const buildRecursiveAudioPlaylist = async (rootNodeId: string): Promise<AudioPlaylistItem[]> => {
  const playlist: AudioPlaylistItem[] = [];
  let foldersSeen = 1;

  for (let depth = 0; depth <= MAX_SCAN_DEPTH; depth += 1) {
    if (foldersSeen >= MAX_FOLDERS_TO_SCAN) {
      break;
    }
    if (playlist.length >= MAX_AUDIO_FILES) {
      break;
    }

    let page = 1;
    let fetched = 0;
    let total = 0;
    let hasNextLevelNodes = false;

    while (true) {
      const response = await nodesApi.getChildren(rootNodeId, {
        page,
        pageSize: SCAN_PAGE_SIZE,
        depth,
      });

      total = response.totalCount;
      fetched += response.content.nodes.length + response.content.files.length;

      if (response.content.nodes.length > 0) {
        hasNextLevelNodes = true;
        foldersSeen += response.content.nodes.length;
      }

      for (const file of response.content.files) {
        if (playlist.length >= MAX_AUDIO_FILES) {
          break;
        }

        if (getFileTypeInfo(file.name, file.contentType).type === "audio") {
          playlist.push({ id: file.id, name: file.name });
        }
      }

      if (fetched >= total) {
        break;
      }

      page += 1;
      // Safety valve in case server misreports totals.
      if (page > 2000) {
        break;
      }
    }

    if (!hasNextLevelNodes) {
      break;
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
