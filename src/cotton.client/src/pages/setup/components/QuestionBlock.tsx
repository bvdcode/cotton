import { Box, Stack } from "@mui/material";
import { type ReactNode } from "react";
import { QuestionHeader } from "./QuestionHeader";
import { OptionCard } from "./OptionCard";
import type { JsonValue } from "../../../shared/types/json";
import type { SetupRenderedOption } from "../setupModels";

interface QuestionBlockProps<TValue extends JsonValue> {
  title: string;
  subtitle: string;
  options: SetupRenderedOption<TValue>[];
  selectedValue?: TValue | null;
  selectedKey?: string | null;
  onSelect: (key: string, value: TValue) => void;
  linkUrl?: string;
  linkAriaLabel?: string;
  extraHeader?: ReactNode;
}

export function QuestionBlock<TValue extends JsonValue>({
  title,
  subtitle,
  options,
  selectedValue,
  selectedKey,
  onSelect,
  linkUrl,
  linkAriaLabel,
  extraHeader,
}: QuestionBlockProps<TValue>) {
  return (
    <Stack spacing={1.5}>
      <QuestionHeader
        title={title}
        subtitle={subtitle}
        linkUrl={linkUrl}
        linkAriaLabel={linkAriaLabel}
        extraHeader={extraHeader}
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
