import { Stack, TextField, FormControlLabel, Checkbox } from "@mui/material";
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
      <Stack spacing={2.5}>
        {fields.map((field) => {
          if (field.type === "boolean") {
            return (
              <FormControlLabel
                key={field.key}
                control={
                  <Checkbox
                    checked={Boolean(values[field.key])}
                    onChange={(e) => onChange(field.key, e.target.checked)}
                  />
                }
                label={field.label}
              />
            );
          }
          
          return (
            <TextField
              key={field.key}
              label={field.label}
              placeholder={field.placeholder}
              type={field.type || "text"}
              value={values[field.key] || ""}
              onChange={(e) => onChange(field.key, e.target.value)}
              fullWidth
              variant="outlined"
            />
          );
        })}
      </Stack>
    </Stack>
  );
}
