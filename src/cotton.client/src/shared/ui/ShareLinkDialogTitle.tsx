import { DialogTitle, Stack, Tooltip, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";

export const ShareLinkDialogTitle = ({
  id,
  name,
}: {
  id: string;
  name: string;
}) => {
  const { t } = useTranslation("files");
  return (
    <DialogTitle id={id} aria-label={`${t("share.options.title")} · ${name}`}>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={{ xs: 0.25, sm: 1 }}
        sx={{ alignItems: { xs: "flex-start", sm: "center" }, minWidth: 0 }}
      >
        <Typography
          component="span"
          variant="inherit"
          noWrap
          sx={{ flexShrink: 0, maxWidth: { xs: "100%", sm: "75%" } }}
        >
          {t("share.options.title")}
        </Typography>
        <Typography
          component="span"
          color="text.secondary"
          aria-hidden
          sx={{ display: { xs: "none", sm: "inline" } }}
        >
          ·
        </Typography>
        <Tooltip title={name}>
          <Typography
            component="span"
            variant="body2"
            color="text.secondary"
            sx={{
              minWidth: 0,
              maxWidth: "100%",
              display: { xs: "-webkit-box", sm: "block" },
              WebkitLineClamp: 2,
              WebkitBoxOrient: "vertical",
              whiteSpace: { xs: "normal", sm: "nowrap" },
              overflow: "hidden",
              textOverflow: "ellipsis",
              overflowWrap: "anywhere",
            }}
          >
            {name}
          </Typography>
        </Tooltip>
      </Stack>
    </DialogTitle>
  );
};
