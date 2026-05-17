import { useQuery, type QueryClient } from "@tanstack/react-query";
import { filesApi } from "../filesApi";
import { nodesApi } from "../nodesApi";
import { parseLrc, type LrcLine } from "../../utils/lrc";
import { queryKeys } from "./queryKeys";

const LYRICS_PAGE_SIZE = 500;
const MAX_LYRICS_PAGES = 200;
const LYRICS_EXPIRE_AFTER_MINUTES = 60 * 24;

const stripExtension = (fileName: string): string => {
  const idx = fileName.lastIndexOf(".");
  if (idx <= 0) return fileName;
  return fileName.slice(0, idx);
};

const normalizeTrackName = (fileName: string): string =>
  stripExtension(fileName).trim().toLowerCase();

const buildInlineTextUrl = (downloadLink: string): string => {
  const url = new URL(downloadLink, window.location.origin);
  url.searchParams.set("download", "false");
  return url.toString();
};

const fetchTrackLyrics = async (
  folderNodeId: string,
  audioFileName: string,
): Promise<LrcLine[] | null> => {
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
      (file) => file.name.trim().toLowerCase() === expectedLrcName,
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

  if (!lrcFileId) {
    return null;
  }

  const downloadLink = await filesApi.getDownloadLink(
    lrcFileId,
    LYRICS_EXPIRE_AFTER_MINUTES,
  );
  const response = await fetch(buildInlineTextUrl(downloadLink));
  if (!response.ok) {
    throw new Error(`Failed to fetch lyrics: ${response.status}`);
  }

  const parsed = parseLrc(await response.text());
  return parsed.length > 0 ? parsed : null;
};

export const clearAudioCaches = (queryClient: QueryClient): void => {
  queryClient.removeQueries({ queryKey: queryKeys.audio.all() });
};

export const useTrackLyricsQuery = (options: {
  folderNodeId: string | null | undefined;
  audioFileName: string | null | undefined;
  enabled?: boolean;
}) => {
  const { folderNodeId, audioFileName } = options;
  const trackName = normalizeTrackName(audioFileName ?? "");
  const enabled =
    (options.enabled ?? true) && !!folderNodeId && trackName.length > 0;

  return useQuery<LrcLine[] | null>({
    queryKey: queryKeys.audio.trackLyrics({
      folderNodeId: folderNodeId ?? "",
      trackName,
    }),
    queryFn: () =>
      fetchTrackLyrics(folderNodeId as string, audioFileName as string),
    enabled,
    staleTime: Infinity,
  });
};
