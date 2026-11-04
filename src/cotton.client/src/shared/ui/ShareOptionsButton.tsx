import { MoreHoriz } from "@mui/icons-material";
import { IconButton, Tooltip } from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { ShareLinkTarget } from "@shared/utils/shareLinkOptions";
import { ShareLinkDialog } from "./ShareLinkDialog";

export const ShareOptionsButton = ({ target }: { target: ShareLinkTarget }) => {
  const { t } = useTranslation("files");
  const [open, setOpen] = useState(false);
  return (
    <>
      <Tooltip title={t("share.options.title")}>
        <IconButton
          size="small"
          aria-label={t("share.options.title")}
          onClick={(event) => {
            event.stopPropagation();
            setOpen(true);
          }}
        >
          <MoreHoriz fontSize="small" />
        </IconButton>
      </Tooltip>
      {open && (
        <ShareLinkDialog target={target} onClose={() => setOpen(false)} />
      )}
    </>
  );
};
