import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("../api/nodesApi", () => ({
  nodesApi: {
    getNode: vi.fn(),
    getChildren: vi.fn(),
  },
}));

import type { NodeDto } from "../api/layoutsApi";
import {
  nodesApi,
  type NodeFileManifestDto,
  type NodeResponse,
} from "../api/nodesApi";
import { useAudioPlayerStore } from "./audioPlayerStore";

const makeNode = (id: string, parentId: string | null, name: string): NodeDto => ({
  id,
  createdAt: "",
  updatedAt: "",
  layoutId: "layout-1",
  parentId,
  name,
  metadata: {},
});

const makeAudioFile = (
  id: string,
  metadata: Record<string, string>,
  fileManifestId = "manifest-1",
): NodeFileManifestDto => ({
  id,
  createdAt: "",
  updatedAt: "",
  nodeId: "root",
  fileManifestId,
  ownerId: "user-1",
  name: "01-song.mp3",
  contentType: "audio/mpeg",
  sizeBytes: 1024,
  metadata,
});

const makeNodeResponse = (
  files: ReadonlyArray<NodeFileManifestDto>,
): NodeResponse => ({
  content: {
    id: "root",
    createdAt: "",
    updatedAt: "",
    nodes: [],
    files: [...files],
  },
  totalCount: files.length,
});

beforeEach(() => {
  vi.clearAllMocks();
  useAudioPlayerStore.getState().reset();
});

describe("audioPlayerStore", () => {
  it("keeps known current track metadata when recursive scan returns a sparse file entry", async () => {
    const mockedNodesApi = vi.mocked(nodesApi);
    mockedNodesApi.getNode.mockResolvedValue(makeNode("root", null, "Music"));
    mockedNodesApi.getChildren.mockResolvedValue(makeNodeResponse([
      makeAudioFile("track-1", {}),
    ]));

    useAudioPlayerStore.getState().openFromSelection({
      fileId: "track-1",
      fileName: "01-song.mp3",
      playlist: [
        {
          id: "track-1",
          fileManifestId: "manifest-1",
          name: "01-song.mp3",
          title: "Known title",
          artist: "Known artist",
          album: "Known album",
          albumArtist: "Known album artist",
          track: "1",
          disc: "1",
          durationSeconds: "180",
          previewUrl: "/api/v1/preview/cover.webp",
        },
      ],
    });
    useAudioPlayerStore.getState().setScanRootNodeId("root");

    await useAudioPlayerStore.getState().scanRecursively();

    const [track] = useAudioPlayerStore.getState().playlist;
    expect(track).toMatchObject({
      id: "track-1",
      fileManifestId: "manifest-1",
      title: "Known title",
      artist: "Known artist",
      album: "Known album",
      albumArtist: "Known album artist",
      track: "1",
      disc: "1",
      durationSeconds: "180",
      previewUrl: "/api/v1/preview/cover.webp",
    });
  });

  it("drops known metadata when recursive scan finds new file content", async () => {
    const mockedNodesApi = vi.mocked(nodesApi);
    mockedNodesApi.getNode.mockResolvedValue(makeNode("root", null, "Music"));
    mockedNodesApi.getChildren.mockResolvedValue(
      makeNodeResponse([makeAudioFile("track-1", {}, "manifest-2")]),
    );

    useAudioPlayerStore.getState().openFromSelection({
      fileId: "track-1",
      fileName: "01-song.mp3",
      playlist: [
        {
          id: "track-1",
          fileManifestId: "manifest-1",
          name: "01-song.mp3",
          title: "Old title",
          artist: "Old artist",
          album: "Old album",
          albumArtist: "Old album artist",
          track: "1",
          disc: "1",
          durationSeconds: "180",
          previewUrl: "/api/v1/preview/old-cover.webp",
        },
      ],
    });
    useAudioPlayerStore.getState().setScanRootNodeId("root");

    await useAudioPlayerStore.getState().scanRecursively();

    const [track] = useAudioPlayerStore.getState().playlist;
    expect(track).toMatchObject({
      id: "track-1",
      fileManifestId: "manifest-2",
      name: "01-song.mp3",
    });
    expect(track?.title).toBeUndefined();
    expect(track?.artist).toBeUndefined();
    expect(track?.album).toBeUndefined();
    expect(track?.albumArtist).toBeUndefined();
    expect(track?.track).toBeUndefined();
    expect(track?.disc).toBeUndefined();
    expect(track?.durationSeconds).toBeUndefined();
    expect(track?.previewUrl).toBeUndefined();
  });
});
