import { Box, Stack } from "@mui/material";
import { QuestionHeader } from "./QuestionHeader";
import { OptionCard } from "./OptionCard";
import type { SetupRenderedMultiOption } from "../setupModels";

interface QuestionBlockMultiProps {
  title: string;
  subtitle: string;
  options: SetupRenderedMultiOption[];
  selectedKeys: string[];
  onToggle: (key: string) => void;
}

export function QuestionBlockMulti({
  title,
  subtitle,
  options,
  selectedKeys,
  onToggle,
}: QuestionBlockMultiProps) {
  return (
    <Stack spacing={1.5}>
      <QuestionHeader title={title} subtitle={subtitle} />
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: options.length === 3 ? "repeat(3, 1fr)" : "repeat(2, 1fr)",
          },
          gap: 1.5,
        }}
      >
        {options.map((opt) => {
          const active = selectedKeys.includes(opt.key);
          return (
            <OptionCard
              key={opt.key}
              label={opt.label}
              description={opt.description}
              icon={opt.icon}
              active={active}
              onClick={() => onToggle(opt.key)}
              disabled={opt.disabled}
              disabledTooltip={opt.disabledTooltip}
            />
          );
        })}
      </Box>
    </Stack>
  );
}
