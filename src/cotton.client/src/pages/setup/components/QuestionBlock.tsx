import { Box, Stack } from "@mui/material";
import { type ReactNode } from "react";
import { QuestionHeader } from "./QuestionHeader";
import { OptionCard } from "./OptionCard";

export function QuestionBlock<T>({
  title,
  subtitle,
  options,
  selectedValue,
  selectedKey,
  onSelect,
  linkUrl,
  linkAriaLabel,
}: {
  title: string;
  subtitle: string;
  options: Array<{
    key: string;
    label: string;
    description?: string;
    value: T;
    icon?: ReactNode;
    disabled?: boolean;
    disabledTooltip?: string;
  }>;
  selectedValue?: T | null;
  selectedKey?: string | null;
  onSelect: (key: string, value: T) => void;
  linkUrl?: string;
  linkAriaLabel?: string;
}) {
  return (
    <Stack spacing={1.5}>
      <QuestionHeader
        title={title}
        subtitle={subtitle}
        linkUrl={linkUrl}
        linkAriaLabel={linkAriaLabel}
      />
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "1fr",
            md: options.length === 3 ? "repeat(3, 1fr)" : "repeat(2, 1fr)",
          },
          gap: { xs: 1.25, sm: 1.5 },
        }}
      >
        {options.map((opt) => {
          const active = selectedKey
            ? selectedKey === opt.key
            : selectedValue === opt.value;
          return (
            <OptionCard
              key={opt.key}
              label={opt.label}
              description={opt.description}
              icon={opt.icon}
              active={active}
              onClick={() => onSelect(opt.key, opt.value)}
              disabled={opt.disabled}
              disabledTooltip={opt.disabledTooltip}
            />
          );
        })}
      </Box>
    </Stack>
  );
}
