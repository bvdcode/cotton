import { Box, Stack } from "@mui/material";
import { type ReactNode } from "react";
import { QuestionHeader } from "./QuestionHeader";
import { OptionCard } from "./OptionCard";

export function QuestionBlockMulti({
  title,
  subtitle,
  options,
  selectedKeys,
  onToggle,
}: {
  title: string;
  subtitle: string;
  options: Array<{
    key: string;
    label: string;
    icon?: ReactNode;
  }>;
  selectedKeys: string[];
  onToggle: (key: string) => void;
}) {
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
              icon={opt.icon}
              active={active}
              onClick={() => onToggle(opt.key)}
            />
          );
        })}
      </Box>
    </Stack>
  );
}
