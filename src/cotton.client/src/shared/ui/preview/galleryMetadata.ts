import type { TFunction } from "i18next";

const metadataLabels: Readonly<Record<string, string>> = {
  "image.format": "format",
  "media.title": "title",
  "media.artist": "artist",
  "media.album": "album",
  "media.albumArtist": "albumArtist",
  "media.track": "track",
  "media.disc": "disc",
  "media.date": "date",
  "media.year": "year",
  "media.genre": "genre",
  "media.audioCodec": "audioCodec",
  "media.videoCodec": "videoCodec",
};

export interface GalleryMetadataEntry {
  key: string;
  label: string;
  value: string;
}

const summaryKeys = new Set([
  "dimensions",
  "duration",
  "image.format",
  "media.date",
  "media.audioCodec",
  "media.videoCodec",
]);

const summaryExifLabels: Readonly<Record<number, string>> = {
  0x010f: "cameraMake",
  0x0110: "cameraModel",
  0x829a: "exposureTime",
  0x829d: "aperture",
  0x8827: "iso",
  0x9003: "dateTaken",
  0x920a: "focalLength",
  0xa434: "lens",
};

export function getGalleryMetadataSections(
  metadata: Record<string, string> | undefined,
  t: TFunction<"files">,
  language: string,
): { summary: GalleryMetadataEntry[]; details: GalleryMetadataEntry[] } {
  const summary: GalleryMetadataEntry[] = [];
  const details: GalleryMetadataEntry[] = [];
  for (const entry of getGalleryMetadataEntries(metadata, t, language)) {
    if (summaryKeys.has(entry.key)) {
      summary.push(entry);
      continue;
    }
    const exif =
      /^image\.(?:Exif IFD0|Exif SubIFD)\.0\.tags\.[^.]+\.(\d+)\.description$/.exec(
        entry.key,
      );
    const label = exif ? summaryExifLabels[Number(exif[1])] : undefined;
    if (label) {
      summary.push({ ...entry, label: t(`preview.metadata.${label}`) });
    } else {
      details.push(entry);
    }
  }
  return { summary, details };
}

const unescapeMetadataKey = (key: string): string =>
  key.replaceAll("~2", "").replaceAll("~1", ".").replaceAll("~0", "~");

const formatMetadataDate = (value: string, language: string): string => {
  if (
    !/^\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:?\d{2})?)?$/i.test(
      value,
    )
  ) {
    return value;
  }
  const hasTime = /T/i.test(value);
  const hasTimeZone = hasTime && /(?:Z|[+-]\d{2}:?\d{2})$/i.test(value);
  const date = new Date(
    hasTimeZone ? value : `${value}${hasTime ? "Z" : "T00:00:00Z"}`,
  );
  if (!Number.isFinite(date.getTime())) {
    return value;
  }
  return new Intl.DateTimeFormat(language, {
    dateStyle: "medium",
    ...(hasTime ? ({ timeStyle: "medium" } as const) : {}),
    ...(hasTimeZone ? {} : { timeZone: "UTC" }),
  }).format(date);
};

export function getGalleryMetadataEntries(
  metadata: Record<string, string> | undefined,
  t: TFunction<"files">,
  language: string,
): GalleryMetadataEntry[] {
  if (!metadata) {
    return [];
  }
  const entries: GalleryMetadataEntry[] = [];
  const width = metadata["image.width"] ?? metadata["media.width"];
  const height = metadata["image.height"] ?? metadata["media.height"];
  if (width && height && Number(width) > 0 && Number(height) > 0) {
    entries.push({
      key: "dimensions",
      label: t("preview.metadata.dimensions"),
      value: `${width} × ${height}`,
    });
  }
  const duration = metadata["media.durationSeconds"];
  if (duration && Number.isFinite(Number(duration)) && Number(duration) >= 0) {
    entries.push({
      key: "duration",
      label: t("preview.metadata.duration"),
      value: t("preview.metadata.seconds", {
        value: new Intl.NumberFormat(language, {
          maximumFractionDigits: 3,
        }).format(Number(duration)),
      }),
    });
  }
  for (const [key, label] of Object.entries(metadataLabels)) {
    const value = metadata[key]?.trim();
    if (value) {
      entries.push({
        key,
        label: t(`preview.metadata.${label}`),
        value:
          key === "media.date" ? formatMetadataDate(value, language) : value,
      });
    }
  }
  for (const [key, raw] of Object.entries(metadata)) {
    const match =
      /^image\.([^.]+)\.\d+\.tags\.([^.]+)\.-?\d+\.description$/.exec(key);
    const value = raw.trim();
    if (match && value) {
      entries.push({
        key,
        label: `${unescapeMetadataKey(match[1])} · ${unescapeMetadataKey(match[2])}`,
        value: formatMetadataDate(value, language),
      });
    }
  }
  return entries;
}
