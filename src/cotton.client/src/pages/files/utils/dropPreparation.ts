type TranslationFn = (
  key: string,
  options?: {
    ns?: string;
    count?: number;
    processed?: number;
    total?: number;
  },
) => string;

export type DropPreparationInfo = {
  phase: "idle" | "scanning" | "preparing";
  step: "idle" | "scanning" | "mapping" | "folders" | "conflicts" | "enqueue";
  filesFound: number;
  processed: number;
};

export const getDropPreparationTitle = (
  t: TranslationFn,
  info: DropPreparationInfo,
): string => {
  const { phase, step } = info;

  if (phase === "scanning") {
    return t("uploadDrop.scanning.title", { ns: "files" });
  }

  if (step === "mapping") {
    return t("uploadDrop.preparing.mapping.title", { ns: "files" });
  }
  if (step === "folders") {
    return t("uploadDrop.preparing.folders.title", { ns: "files" });
  }
  if (step === "conflicts") {
    return t("uploadDrop.preparing.conflicts.title", { ns: "files" });
  }
  if (step === "enqueue") {
    return t("uploadDrop.preparing.enqueue.title", { ns: "files" });
  }

  return t("uploadDrop.preparing.title", { ns: "files" });
};

export const getDropPreparationCaption = (
  t: TranslationFn,
  info: DropPreparationInfo,
): string => {
  const { phase, filesFound, processed } = info;

  const found = t("uploadDrop.captionFound", {
    ns: "files",
    count: filesFound,
  });

  if (phase === "scanning") return found;
  if (filesFound <= 0) return found;

  const progress = t("uploadDrop.captionProgress", {
    ns: "files",
    processed: Math.max(0, Math.min(filesFound, processed)),
    total: filesFound,
  });

  return `${found} • ${progress}`;
};
