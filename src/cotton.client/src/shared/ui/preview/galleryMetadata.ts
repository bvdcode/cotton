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
