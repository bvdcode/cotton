import { AutoAwesome } from "@mui/icons-material";
import { IconButton, Tooltip } from "@mui/material";
import { useTranslation } from "react-i18next";

interface DeepSearchButtonProps {
  enabled: boolean;
  disabled?: boolean;
  onClick: () => void;
}

export const DeepSearchButton = ({
  enabled,
  disabled,
  onClick,
}: DeepSearchButtonProps) => {
  const { t } = useTranslation("search");

  return (
    <Tooltip title={t(enabled ? "smartSearch.disable" : "smartSearch.enable")}>
      <span>
        <IconButton
          aria-label={t("smartSearch.label")}
          aria-pressed={enabled}
          sx={
            enabled
              ? {
                  bgcolor: "primary.main",
                  color: "primary.contrastText",
                  "&:hover": { bgcolor: "primary.dark" },
                }
              : undefined
          }
          disabled={disabled}
          onClick={onClick}
        >
          <AutoAwesome />
        </IconButton>
      </span>
    </Tooltip>
  );
};
