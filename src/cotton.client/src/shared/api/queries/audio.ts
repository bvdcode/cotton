import { useQuery, type QueryClient } from "@tanstack/react-query";
import { filesApi } from "../filesApi";
import { nodesApi } from "../nodesApi";
import { parseLrc, type LrcLine } from "../../utils/lrc";
import { parseSrt } from "../../utils/srt";
import { queryKeys } from "./queryKeys";

const LYRICS_PAGE_SIZE = 500;
const MAX_LYRICS_PAGES = 200;
const LYRICS_EXPIRE_AFTER_MINUTES = 60 * 24;
const TEXT_TRACK_EXTENSIONS = [".lrc", ".srt"] as const;

type TextTrackExtension = (typeof TEXT_TRACK_EXTENSIONS)[number];

interface MatchedTextTrack {
  fileId: string;
  extension: TextTrackExtension;
}

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

const parseTextTrack = (
  extension: TextTrackExtension,
  content: string,
): LrcLine[] => {
  if (extension === ".srt") {
    return parseSrt(content);
  }

  return parseLrc(content);
};

const fetchTrackLyrics = async (
  folderNodeId: string,
  audioFileName: string,
): Promise<LrcLine[] | null> => {
  const baseName = stripExtension(audioFileName).trim().toLowerCase();
  const candidates = TEXT_TRACK_EXTENSIONS.map((extension) => ({
    extension,
    fileName: `${baseName}${extension}`,
  }));

  let page = 1;
  let fetched = 0;
  let bestMatch: MatchedTextTrack | null = null;
  let bestRank: number = TEXT_TRACK_EXTENSIONS.length;

  while (page <= MAX_LYRICS_PAGES) {
    const response = await nodesApi.getChildren(folderNodeId, {
      page,
      pageSize: LYRICS_PAGE_SIZE,
      depth: 0,
    });

    const total = response.totalCount;
    fetched += response.content.nodes.length + response.content.files.length;

    for (const file of response.content.files) {
      const fileName = file.name.trim().toLowerCase();
      for (let rank = 0; rank < bestRank; rank += 1) {
        const candidate = candidates[rank];
        if (candidate && fileName === candidate.fileName) {
          bestMatch = {
            fileId: file.id,
            extension: candidate.extension,
          };
          bestRank = rank;
          break;
        }
      }
    }

    if (bestRank === 0) {
      break;
    }

    if (fetched >= total) {
      break;
    }

    page += 1;
  }

  if (!bestMatch) {
    return null;
  }

  const downloadLink = await filesApi.getDownloadLink(
    bestMatch.fileId,
    LYRICS_EXPIRE_AFTER_MINUTES,
  );
  const response = await fetch(buildInlineTextUrl(downloadLink));
  if (!response.ok) {
    throw new Error(`Failed to fetch lyrics: ${response.status}`);
  }

  const parsed = parseTextTrack(bestMatch.extension, await response.text());
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
