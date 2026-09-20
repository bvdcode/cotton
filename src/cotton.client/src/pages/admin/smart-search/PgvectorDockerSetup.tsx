import { Link, MenuItem, Stack, TextField, Typography } from "@mui/material";
import { useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { SetupCommand } from "./SetupCommand";
import {
  getPgvectorDockerImage,
  PGVECTOR_DOCUMENTATION,
} from "./smartSearchSetup";

export const PgvectorDockerSetup = ({
  majorVersion,
  environmentControl,
  action,
}: {
  majorVersion: number;
  environmentControl: ReactNode;
  action: ReactNode;
}) => {
  const { t } = useTranslation("admin");
  const [currentImage, setCurrentImage] = useState("");
  const [variant, setVariant] = useState("");
  const result = getPgvectorDockerImage(currentImage, majorVersion, variant);
  const showVariant =
    result.kind === "chooseVariant" ||
    (result.kind === "ready" &&
      !/-(bookworm|trixie)$/.test(currentImage.trim()));
  let error: string | null = null;
  switch (result.kind) {
    case "invalid":
      error = t("smartSearch.installation.imageUnknown", {
        image: `postgres:${majorVersion}-bookworm`,
      });
      break;
    case "unsupported":
      error = t("smartSearch.installation.imageUnsupported");
      break;
    case "versionUnsupported":
      error = t("smartSearch.installation.versionUnsupported", {
        version: majorVersion,
      });
      break;
    case "mismatch":
      error = t("smartSearch.installation.imageMismatch", {
        version: majorVersion,
      });
      break;
    case "empty":
    case "chooseVariant":
    case "ready":
      break;
  }

  return (
    <Stack spacing={1.5}>
      <Stack direction={{ xs: "column", sm: "row" }} gap={1.5}>
        {environmentControl}
        <TextField
          size="small"
          fullWidth
          label={t("smartSearch.installation.image")}
          placeholder={`postgres:${majorVersion}-bookworm`}
          value={currentImage}
          error={error !== null}
          onChange={(event) => {
            setCurrentImage(event.target.value);
            setVariant("");
          }}
          helperText={error}
        />
      </Stack>
      {showVariant && (
        <TextField
          select
          size="small"
          fullWidth
          label={t("smartSearch.installation.variant")}
          helperText={t("smartSearch.installation.variantHint")}
          value={variant}
          onChange={(event) => setVariant(event.target.value)}
        >
          <MenuItem value="bookworm">Debian 12 (bookworm)</MenuItem>
          <MenuItem value="trixie">Debian 13 (trixie)</MenuItem>
        </TextField>
      )}
      {result.kind === "ready" && (
        <Stack spacing={0.5}>
          <Typography variant="body2">
            {t("smartSearch.installation.replace")}
          </Typography>
          <SetupCommand command={`image: ${result.image}`} />
        </Stack>
      )}
      {result.kind === "ready" && (
        <Typography variant="body2" color="text.secondary">
          {t("smartSearch.installation.imageAbout")}{" "}
          {t("smartSearch.installation.dockerApply")}
        </Typography>
      )}
      <Stack direction="row" flexWrap="wrap" alignItems="center" gap={2}>
        {action}
        <Link
          href={`${PGVECTOR_DOCUMENTATION}#docker`}
          target="_blank"
          rel="noreferrer"
          variant="body2"
          color="text.primary"
        >
          {t("smartSearch.installation.docs")}
        </Link>
      </Stack>
    </Stack>
  );
};
