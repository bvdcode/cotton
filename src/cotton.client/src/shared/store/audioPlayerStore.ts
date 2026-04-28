import { create } from "zustand";
import { filesApi } from "../api/filesApi";
import { nodesApi } from "../api/nodesApi";
import type { AudioPlaylistItem } from "../types/audio";
import { getFileTypeInfo } from "../../pages/files/utils/fileTypes";
import { parseLrc, type LrcLine } from "../utils/lrc";

interface AudioPlayerState {
  open: boolean;
  isScanning: boolean;

  shuffleEnabled: boolean;

  scanRootNodeId: string | null;

  playlist: ReadonlyArray<AudioPlaylistItem>;
  currentFileId: string | null;
  currentFileName: string | null;

  lyricsOpen: boolean;
  lyricsStatus: LyricsStatus;
  lyricsLines: ReadonlyArray<LrcLine>;
  lyricsTrackKey: string | null;

  lyricsCache: Record<string, ReadonlyArray<LrcLine> | null>;
  lyricsRequestId: number;

  openFromSelection: (args: {
    fileId: string;
    fileName: string;
    playlist?: ReadonlyArray<AudioPlaylistItem> | null;
  }) => void;

  toggleShuffle: () => void;

  toggleLyricsOpen: () => void;
  loadLyricsForTrack: (args: {
    folderNodeId?: string | null;
    audioFileName?: string | null;
  }) => Promise<void>;

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

type LyricsStatus = "idle" | "loading" | "ready" | "notFound" | "error";

const LYRICS_PAGE_SIZE = 500;
const MAX_LYRICS_PAGES = 200;
const LYRICS_EXPIRE_AFTER_MINUTES = 60 * 24;

const stripExtension = (fileName: string): string => {
  const idx = fileName.lastIndexOf(".");
  if (idx <= 0) return fileName;
  return fileName.slice(0, idx);
};

const PLAYLIST_COLLATOR = new Intl.Collator(undefined, {
  numeric: true,
  sensitivity: "base",
});

const normalizeTrackName = (name: string): string => stripExtension(name).trim();

const compareNullableStrings = (a: string | null, b: string | null): number => {
  if (a === b) return 0;
  if (a === null) return -1;
  if (b === null) return 1;
  return PLAYLIST_COLLATOR.compare(a, b);
};

const sortAudioPlaylist = (
  items: ReadonlyArray<AudioPlaylistItem>,
): AudioPlaylistItem[] => {
  if (items.length <= 1) {
    return items.slice();
  }

  const next = items.slice();
  next.sort((left, right) => {
    const folderCompare = compareNullableStrings(
      left.folderPath ?? null,
      right.folderPath ?? null,
    );
    if (folderCompare !== 0) return folderCompare;

    return PLAYLIST_COLLATOR.compare(
      normalizeTrackName(left.name),
      normalizeTrackName(right.name),
    );
  });
  return next;
};

const buildLyricsKey = (folderNodeId: string, audioFileName: string): string => {
  const base = stripExtension(audioFileName).trim().toLowerCase();
  return `${folderNodeId}:${base}`;
};

const buildInlineTextUrl = (downloadLink: string): string => {
  const url = new URL(downloadLink, window.location.origin);
  url.searchParams.set("download", "false");
  return url.toString();
};

const hasOwn = (
  obj: Record<string, ReadonlyArray<LrcLine> | null>,
  key: string,
): boolean => Object.prototype.hasOwnProperty.call(obj, key);

type NodeInfo = {
  id: string;
  parentId: string | null;
  name: string;
};

const tryBuildPreviewUrl = (token: string | null | undefined): string | undefined => {
  if (!token) return undefined;
  return `/api/v1/preview/${encodeURIComponent(token)}.webp`;
};

const buildRecursiveAudioPlaylist = async (rootNodeId: string): Promise<AudioPlaylistItem[]> => {
  const playlist: AudioPlaylistItem[] = [];
  let foldersSeen = 1;

  const rootNode = await nodesApi.getNode(rootNodeId);

  const nodeMap = new Map<string, NodeInfo>();
  nodeMap.set(rootNode.id, {
    id: rootNode.id,
    parentId: rootNode.parentId,
    name: rootNode.name,
  });

  const folderPathCache = new Map<string, string>();

  const buildFolderPath = (nodeId: string): string | null => {
    const cached = folderPathCache.get(nodeId);
    if (cached) {
      return cached;
    }

    if (nodeId === rootNodeId) {
      // Root itself is not shown in the UI.
      return null;
    }

    const parts: string[] = [];
    let current: string | null = nodeId;
    let guard = 0;

    while (current && current !== rootNodeId) {
      if (guard++ > MAX_SCAN_DEPTH + 5) {
        break;
      }

      const info = nodeMap.get(current);
      if (!info) {
        return null;
      }

      parts.push(info.name);
      current = info.parentId;
    }

    parts.reverse();
    const rel = parts.filter((p) => p.trim().length > 0).join("/");

    if (rel) {
      folderPathCache.set(nodeId, rel);
      return rel;
    }

    return null;
  };

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

      for (const node of response.content.nodes) {
        if (nodeMap.size >= MAX_FOLDERS_TO_SCAN) {
          break;
        }
        nodeMap.set(node.id, {
          id: node.id,
          parentId: node.parentId,
          name: node.name,
        });
      }

      for (const file of response.content.files) {
        if (playlist.length >= MAX_AUDIO_FILES) {
          break;
        }

        if (getFileTypeInfo(file.name, file.contentType).type === "audio") {
          const folderPath = file.nodeId ? buildFolderPath(file.nodeId) : null;
          playlist.push({
            id: file.id,
            name: file.name,
            nodeId: file.nodeId,
            folderPath: folderPath ?? undefined,
            previewUrl: tryBuildPreviewUrl(
              file.previewHashEncryptedHex,
            ),
          });
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
  shuffleEnabled: false,
  scanRootNodeId: null,
  playlist: [],
  currentFileId: null,
  currentFileName: null,

  lyricsOpen: false,
  lyricsStatus: "idle",
  lyricsLines: [],
  lyricsTrackKey: null,
  lyricsCache: {},
  lyricsRequestId: 0,

  openFromSelection: ({ fileId, fileName, playlist }) => {
    const effectivePlaylist = (playlist ?? []).length
      ? (playlist ?? [])
      : [{ id: fileId, name: fileName }];

    set({
      open: true,
      currentFileId: fileId,
      currentFileName: fileName,
      playlist: sortAudioPlaylist(effectivePlaylist),
    });
  },

  toggleShuffle: () => {
    set((prev) => ({ shuffleEnabled: !prev.shuffleEnabled }));
  },

  toggleLyricsOpen: () => {
    set((prev) => {
      if (prev.lyricsOpen) {
        return {
          lyricsOpen: false,
          lyricsStatus: "idle" as const,
          lyricsLines: [],
          lyricsTrackKey: null,
        };
      }

      return { lyricsOpen: true };
    });
  },

  loadLyricsForTrack: async ({ folderNodeId, audioFileName }) => {
    if (!folderNodeId || !audioFileName) {
      set({
        lyricsStatus: "notFound",
        lyricsLines: [],
        lyricsTrackKey: null,
      });
      return;
    }

    const key = buildLyricsKey(folderNodeId, audioFileName);
    const state = get();
    if (state.lyricsTrackKey === key && state.lyricsStatus === "ready") {
      return;
    }

    if (hasOwn(state.lyricsCache, key)) {
      const cached = state.lyricsCache[key] ?? null;
      set({
        lyricsTrackKey: key,
        lyricsStatus: cached ? "ready" : "notFound",
        lyricsLines: cached ?? [],
      });
      return;
    }

    const requestId = state.lyricsRequestId + 1;
    set({
      lyricsTrackKey: key,
      lyricsStatus: "loading",
      lyricsLines: [],
      lyricsRequestId: requestId,
    });

    try {
      const expectedLrcName = `${stripExtension(audioFileName)}.lrc`
        .trim()
        .toLowerCase();

      let page = 1;
      let fetched = 0;
      let total = 0;
      let lrcFileId: string | null = null;

      while (page <= MAX_LYRICS_PAGES) {
        const response = await nodesApi.getChildren(folderNodeId, {
          page,
          pageSize: LYRICS_PAGE_SIZE,
          depth: 0,
        });

        total = response.totalCount;
        fetched += response.content.nodes.length + response.content.files.length;

        const match = response.content.files.find(
          (f) => f.name.trim().toLowerCase() === expectedLrcName,
        );
        if (match) {
          lrcFileId = match.id;
          break;
        }

        if (fetched >= total) {
          break;
        }

        page += 1;
      }

      let lines: ReadonlyArray<LrcLine> | null = null;
      if (lrcFileId) {
        const downloadLink = await filesApi.getDownloadLink(
          lrcFileId,
          LYRICS_EXPIRE_AFTER_MINUTES,
        );
        const url = buildInlineTextUrl(downloadLink);
        const response = await fetch(url);
        if (!response.ok) {
          throw new Error(`Failed to fetch lyrics: ${response.status}`);
        }

        const content = await response.text();
        const parsed = parseLrc(content);
        lines = parsed.length > 0 ? parsed : null;
      }

      set((prev) => {
        if (prev.lyricsRequestId !== requestId) {
          return {};
        }

        return {
          lyricsCache: { ...prev.lyricsCache, [key]: lines },
          lyricsStatus: lines ? "ready" : "notFound",
          lyricsLines: lines ?? [],
        };
      });
    } catch {
      set((prev) => {
        if (prev.lyricsRequestId !== requestId) {
          return {};
        }

        return {
          lyricsStatus: "error",
          lyricsLines: [],
        };
      });
    }
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
          const existing = get().playlist.find((x) => x.id === currentId);
          next.unshift({
            id: currentId,
            name: currentName,
            previewUrl: existing?.previewUrl,
          });
        }
      }

      set({ playlist: sortAudioPlaylist(next) });
    } finally {
      set({ isScanning: false });
    }
  },

  close: () =>
    set({
      open: false,
      lyricsOpen: false,
      lyricsStatus: "idle",
      lyricsLines: [],
      lyricsTrackKey: null,
    }),

  reset: () =>
    set({
      open: false,
      isScanning: false,
      shuffleEnabled: false,
      scanRootNodeId: null,
      playlist: [],
      currentFileId: null,
      currentFileName: null,
      lyricsOpen: false,
      lyricsStatus: "idle",
      lyricsLines: [],
      lyricsTrackKey: null,
      lyricsCache: {},
      lyricsRequestId: 0,
    }),
}));

export const selectAudioPlayerOpen = (s: AudioPlayerState): boolean => s.open;
export const selectAudioPlayerIsScanning = (s: AudioPlayerState): boolean => s.isScanning;
export const selectAudioPlayerShuffleEnabled = (s: AudioPlayerState): boolean => s.shuffleEnabled;
export const selectAudioPlayerPlaylist = (s: AudioPlayerState): ReadonlyArray<AudioPlaylistItem> => s.playlist;
export const selectAudioPlayerCurrentFileId = (s: AudioPlayerState): string | null => s.currentFileId;
export const selectAudioPlayerCurrentFileName = (s: AudioPlayerState): string | null => s.currentFileName;
export const selectAudioPlayerLyricsOpen = (s: AudioPlayerState): boolean => s.lyricsOpen;
export const selectAudioPlayerLyricsStatus = (s: AudioPlayerState): LyricsStatus => s.lyricsStatus;
export const selectAudioPlayerLyricsLines = (
  s: AudioPlayerState,
): ReadonlyArray<LrcLine> => s.lyricsLines;
