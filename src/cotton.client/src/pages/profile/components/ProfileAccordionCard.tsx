import { type ReactElement, type ReactNode } from "react";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Box,
  Divider,
  Typography,
  type SxProps,
  type Theme,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";

type ProfileAccordionCardProps = {
  id: string;
  ariaControls: string;
  icon: ReactElement;
  title: ReactNode;
  description?: ReactNode;
  count?: number;
  children: ReactNode;
  defaultExpanded?: boolean;
  showDivider?: boolean;
  sx?: SxProps<Theme>;
  summarySx?: SxProps<Theme>;
  detailsSx?: SxProps<Theme>;
};

export const ProfileAccordionCard = ({
  id,
  ariaControls,
  icon,
  title,
  description,
  count,
  children,
  defaultExpanded,
  showDivider = true,
  sx,
  summarySx,
  detailsSx,
}: ProfileAccordionCardProps) => {
  return (
    <Accordion
      disableGutters
      defaultExpanded={defaultExpanded}
      sx={[
        {
          borderRadius: 1,
          overflow: "hidden",
        },
        ...(Array.isArray(sx) ? sx : sx ? [sx] : []),
      ]}
    >
      <AccordionSummary
        expandIcon={<ExpandMoreIcon />}
        aria-controls={ariaControls}
        id={id}
        sx={[
          {
            minHeight: { xs: 56, sm: 64 },
            px: { xs: 2, sm: 3 },
            py: { xs: 1.25, sm: 1.5 },
            "& .MuiAccordionSummary-content": {
              margin: 0,
              minWidth: 0,
            },
            "& .MuiAccordionSummary-expandIconWrapper": {
              color: "text.secondary",
            },
          },
          ...(Array.isArray(summarySx)
            ? summarySx
            : summarySx
              ? [summarySx]
              : []),
        ]}
      >
        <Box sx={{ width: "100%", display: "flex", alignItems: "center", gap: 2, minWidth: 0 }}>
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, minWidth: 0 }}>
              {icon}
              <Typography variant="h6" fontWeight={600} noWrap>
                {title}
              </Typography>
            </Box>

            {description && (
              <Typography
                variant="body2"
                color="text.secondary"
                sx={{ fontSize: "0.9rem", mt: 0.5 }}
              >
                {description}
              </Typography>
            )}
          </Box>

          {typeof count === "number" && (
            <Typography variant="body2" color="text.secondary" sx={{ whiteSpace: "nowrap" }}>
              {count}
            </Typography>
          )}
        </Box>
      </AccordionSummary>

      {showDivider && <Divider sx={{ mx: { xs: 0, sm: 3 } }} />}

      <AccordionDetails
        sx={[
          {
            px: { xs: 2, sm: 3 },
            pb: { xs: 2, sm: 2 },
          },
          ...(Array.isArray(detailsSx)
            ? detailsSx
            : detailsSx
              ? [detailsSx]
              : []),
        ]}
      >
        {children}
      </AccordionDetails>
    </Accordion>
  );
};
