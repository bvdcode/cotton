import { IconButton, Tooltip } from "@mui/material";
import HelpOutlineIcon from "@mui/icons-material/HelpOutline";
import { useConfirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import type { MouseEvent, ReactNode } from "react";

type Props = {
  title: string;
  content: ReactNode;
  tooltip?: string;
  size?: "small" | "medium";
};

export const HelpButton = ({
  title,
  content,
  tooltip = title,
  size = "small",
}: Props) => {
  const { t } = useTranslation("common");
  const confirm = useConfirm();
  const showHelp = (event: MouseEvent) => {
    event.stopPropagation();
    event.preventDefault();
    void confirm({
      title,
      content,
      hideCancelButton: true,
      confirmationText: t("actions.close"),
      confirmationButtonProps: { color: "inherit" },
    });
  };

  return (
    <Tooltip title={tooltip}>
      <IconButton size={size} onClick={showHelp} aria-label={tooltip}>
        <HelpOutlineIcon fontSize={size} />
      </IconButton>
    </Tooltip>
  );
};
