import { Box, Stack, Tooltip } from "@mui/material";
import { useTranslation } from "react-i18next";
import { InfoRow } from "./InfoRow";
import { formatDateTime } from "./userInfoCardFormatters";

interface UserInfoMetadataProps {
  userId: string;
  username: string;
  createdAt: string;
}

export const UserInfoMetadata = ({
  userId,
  username,
  createdAt,
}: UserInfoMetadataProps) => {
  const { t } = useTranslation(["profile"]);

  return (
    <Stack
      direction={{ xs: "column", sm: "row" }}
      spacing={{ xs: 1.5, sm: 3 }}
    >
      <Stack spacing={1.25} flex={1}>
        <InfoRow
          label={t("fields.username")}
          value={
            <Tooltip title={userId} arrow>
              <Box component="span">{username}</Box>
            </Tooltip>
          }
        />
      </Stack>

      <Stack spacing={1.25} flex={1}>
        <InfoRow label={t("fields.createdAt")} value={formatDateTime(createdAt)} />
      </Stack>
    </Stack>
  );
};