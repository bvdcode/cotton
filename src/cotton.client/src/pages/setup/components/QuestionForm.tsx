import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import RadioButtonUncheckedIcon from "@mui/icons-material/RadioButtonUnchecked";
import { Box, Stack, TextField, ToggleButton, Typography } from "@mui/material";
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
            md: "repeat(2, minmax(0, 1fr))",
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
                <Box
                  component="span"
                  sx={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: 0.75,
                    color: selected ? "primary.main" : "text.secondary",
                  }}
                >
                  <Typography component="span" variant="caption" fontWeight={800}>
                    {selected ? "On" : "Off"}
                  </Typography>
                  {selected ? (
                    <CheckCircleOutlineIcon fontSize="small" />
                  ) : (
                    <RadioButtonUncheckedIcon fontSize="small" />
                  )}
                </Box>
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
