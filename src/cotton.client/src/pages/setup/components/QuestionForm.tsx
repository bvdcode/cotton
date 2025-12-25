import { Stack, TextField, useTheme } from "@mui/material";
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
    type?: "text" | "password" | "url";
  }>;
  values: Record<string, string>;
  onChange: (key: string, value: string) => void;
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
  const theme = useTheme();

  return (
    <Stack spacing={3}>
      <QuestionHeader
        title={title}
        subtitle={subtitle}
        linkUrl={linkUrl}
        linkAriaLabel={linkAriaLabel}
      />
      <Stack spacing={2.5}>
        {fields.map((field) => (
          <TextField
            key={field.key}
            label={field.label}
            placeholder={field.placeholder}
            type={field.type || "text"}
            value={values[field.key] || ""}
            onChange={(e) => onChange(field.key, e.target.value)}
            fullWidth
            variant="outlined"
            sx={{
              "& .MuiOutlinedInput-root": {
                bgcolor: theme.palette.background.paper,
                "&:hover": {
                  bgcolor: theme.palette.action.hover,
                },
                "&.Mui-focused": {
                  bgcolor: theme.palette.background.paper,
                },
              },
            }}
          />
        ))}
      </Stack>
    </Stack>
  );
}
