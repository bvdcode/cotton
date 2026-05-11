import { Stack, Typography } from "@mui/material";
import { useEffect, useRef, useState, type ReactNode } from "react";
import type { SaveStatus } from "./useAutoSavedSetting";
import { AdminSettingStatusIndicator } from "./AdminSettingStatusIndicator";

type SettingsSectionProps = {
  title: ReactNode;
  titleAction?: ReactNode;
  description?: ReactNode;
  status?: SaveStatus;
  action?: ReactNode;
  children?: ReactNode;
  highlight?: boolean;
  highlightKey?: string;
};

export const SettingsSection = ({
  title,
  titleAction,
  description,
  status = "idle",
  action,
  children,
  highlight = false,
  highlightKey,
}: SettingsSectionProps) => {
  const sectionRef = useRef<HTMLDivElement | null>(null);
  const [highlightVisible, setHighlightVisible] = useState(false);

  useEffect(() => {
    if (!highlight) {
      setHighlightVisible(false);
      return;
    }

    setHighlightVisible(true);

    const handle = window.setTimeout(() => {
      sectionRef.current?.scrollIntoView({
        behavior: "smooth",
        block: "center",
      });
    }, 80);

    const fadeHandle = window.setTimeout(() => {
      setHighlightVisible(false);
    }, 2600);

    return () => {
      window.clearTimeout(handle);
      window.clearTimeout(fadeHandle);
    };
  }, [highlight, highlightKey]);

  return (
    <Stack
      ref={sectionRef}
      spacing={1.25}
    >
      <Stack
        direction="row"
        spacing={1}
        alignItems="flex-start"
        justifyContent="space-between"
      >
        <Stack direction="column" spacing={0.25} minWidth={0} flex={1}>
          <Stack
            direction="row"
            spacing={1}
            alignItems="center"
            minWidth={0}
          >
            <Typography
              variant="subtitle1"
              fontWeight={700}
              color={highlightVisible ? "primary.main" : "text.primary"}
              sx={(theme) => ({
                transition: theme.transitions.create("color", {
                  duration: theme.transitions.duration.shorter,
                }),
              })}
            >
              {title}
            </Typography>
            {titleAction}
            <AdminSettingStatusIndicator status={status} />
          </Stack>
          {description && (
            <Typography variant="caption" color="text.secondary">
              {description}
            </Typography>
          )}
        </Stack>
        {action}
      </Stack>
      {children}
    </Stack>
  );
};
