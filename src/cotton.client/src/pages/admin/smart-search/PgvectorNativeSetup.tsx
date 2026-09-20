import { Link, MenuItem, Stack, TextField, Typography } from "@mui/material";
import { useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { SetupCommand } from "./SetupCommand";
import {
  PGVECTOR_DOCUMENTATION,
  PGVECTOR_RELEASE,
  SUPPORTED_POSTGRES_VERSIONS,
} from "./smartSearchSetup";

export const PgvectorNativeSetup = ({
  majorVersion,
  environmentControl,
  action,
}: {
  majorVersion: number;
  environmentControl: ReactNode;
  action: ReactNode;
}) => {
  const { t } = useTranslation("admin");
  const [platform, setPlatform] = useState<
    "apt" | "rpm" | "mac" | "windows" | ""
  >("");
  const [postgresPath, setPostgresPath] = useState(
    `C:\\Program Files\\PostgreSQL\\${majorVersion}`,
  );
  const validPostgresPath = /^[A-Za-z]:\\[^"\r\n%]+$/.test(postgresPath.trim());
  let command: string | null = null;
  let repository: string | null = null;
  let note: string | null = null;
  let documentation = `${PGVECTOR_DOCUMENTATION}#installation`;
  if (SUPPORTED_POSTGRES_VERSIONS.includes(majorVersion)) {
    switch (platform) {
      case "apt":
        command = `sudo apt install postgresql-${majorVersion}-pgvector`;
        repository = "https://wiki.postgresql.org/wiki/Apt";
        break;
      case "rpm":
        command = `sudo dnf install pgvector_${majorVersion}`;
        repository = "https://www.postgresql.org/download/linux/redhat/";
        break;
      case "mac":
        documentation = `${PGVECTOR_DOCUMENTATION}#homebrew`;
        if (majorVersion === 17 || majorVersion === 18) {
          command = "brew install pgvector";
        } else {
          note = t("smartSearch.installation.macVersion");
        }
        break;
      case "windows":
        documentation = `${PGVECTOR_DOCUMENTATION}#windows`;
        note = t("smartSearch.installation.windowsNote");
        if (validPostgresPath) {
          command = [
            `set "PGROOT=${postgresPath.trim()}"`,
            "cd %TEMP%",
            `git clone --branch v${PGVECTOR_RELEASE} https://github.com/pgvector/pgvector.git`,
            "cd pgvector",
            "nmake /F Makefile.win",
            "nmake /F Makefile.win install",
          ].join("\n");
        }
        break;
      case "":
        break;
    }
  }

  return (
    <Stack spacing={1.5}>
      <Stack direction={{ xs: "column", sm: "row" }} gap={1.5}>
        {environmentControl}
        <TextField
          select
          size="small"
          fullWidth
          label={t("smartSearch.installation.os")}
          value={platform}
          onChange={(event) => {
            const value = event.target.value;
            if (
              value === "apt" ||
              value === "rpm" ||
              value === "mac" ||
              value === "windows"
            ) {
              setPlatform(value);
            }
          }}
        >
          <MenuItem value="apt">Debian / Ubuntu</MenuItem>
          <MenuItem value="rpm">RHEL / Rocky Linux / AlmaLinux</MenuItem>
          <MenuItem value="mac">macOS (Homebrew)</MenuItem>
          <MenuItem value="windows">Windows</MenuItem>
        </TextField>
      </Stack>
      {platform === "windows" && (
        <TextField
          fullWidth
          size="small"
          label={t("smartSearch.installation.windowsPath")}
          value={postgresPath}
          onChange={(event) => setPostgresPath(event.target.value)}
          error={!validPostgresPath}
        />
      )}
      {note && <Typography variant="body2">{note}</Typography>}
      {command && <SetupCommand command={command} />}
      {!SUPPORTED_POSTGRES_VERSIONS.includes(majorVersion) && (
        <Typography variant="body2">
          {t("smartSearch.installation.versionUnsupported", {
            version: majorVersion,
          })}
        </Typography>
      )}
      <Stack direction="row" flexWrap="wrap" alignItems="center" gap={2}>
        {action}
        {repository && (
          <Link
            href={repository}
            target="_blank"
            rel="noreferrer"
            variant="body2"
            color="text.primary"
          >
            {t("smartSearch.installation.repository")}
          </Link>
        )}
        <Link
          href={documentation}
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
