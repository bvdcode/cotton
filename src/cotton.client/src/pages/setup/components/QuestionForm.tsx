import { Box, Chip, Stack, TextField, ToggleButton } from "@mui/material";
import { QuestionHeader } from "./QuestionHeader";

type QuestionFormProps = {
  title: string;
  subtitle: string;
  linkUrl?: string;
  linkAriaLabel?: string;
  fields: Array<{
    key: string;
    label: string;
    placeholder?: string;
    type?: "text" | "password" | "url" | "boolean";
  }>;
  values: Record<string, string | boolean>;
  onChange: (key: string, value: string | boolean) => void;
};

export function QuestionForm({
  title,
  subtitle,
  linkUrl,
  linkAriaLabel,
  fields,
  values,
  onChange,
}: QuestionFormProps) {
  return (
    <Stack spacing={3}>
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
            md:
              fields.length > 1
                ? "repeat(2, minmax(0, 1fr))"
                : "1fr",
          },
          gap: 2.5,
        }}
      >
        {fields.map((field) => {
          if (field.type === "boolean") {
            const selected = Boolean(values[field.key]);

            return (
              <ToggleButton
                key={field.key}
                value={field.key}
                selected={selected}
                onChange={() => onChange(field.key, !selected)}
                fullWidth
                sx={{
                  minHeight: 56,
                  justifyContent: "space-between",
                  px: 1.75,
                  textTransform: "none",
                  fontWeight: 700,
                }}
              >
                {field.label}
                <Chip
                  label={selected ? "On" : "Off"}
                  color={selected ? "primary" : "default"}
                  size="small"
                  variant={selected ? "filled" : "outlined"}
                />
              </ToggleButton>
            );
          }

          const value = values[field.key];
          return (
            <TextField
              key={field.key}
              label={field.label}
              placeholder={field.placeholder}
              type={field.type || "text"}
              value={typeof value === "string" ? value : ""}
              onChange={(e) => onChange(field.key, e.target.value)}
              fullWidth
              variant="outlined"
            />
          );
        })}
      </Box>
    </Stack>
  );
}
