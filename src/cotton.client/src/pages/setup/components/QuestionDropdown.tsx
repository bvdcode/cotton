import { Stack, FormControl, InputLabel, Select, MenuItem } from "@mui/material";
import { QuestionHeader } from "./QuestionHeader";

type QuestionDropdownProps = {
  title: string;
  subtitle: string;
  linkUrl?: string;
  linkAriaLabel?: string;
  options: Array<{
    key: string;
    label: string;
    value: unknown;
  }>;
  selectedKey: string | null;
  onSelect: (key: string) => void;
};

export function QuestionDropdown({
  title,
  subtitle,
  linkUrl,
  linkAriaLabel,
  options,
  selectedKey,
  onSelect,
}: QuestionDropdownProps) {
  return (
    <Stack spacing={3}>
      <QuestionHeader
        title={title}
        subtitle={subtitle}
        linkUrl={linkUrl}
        linkAriaLabel={linkAriaLabel}
      />
      <FormControl fullWidth>
        <InputLabel id="question-dropdown-label">{title}</InputLabel>
        <Select
          labelId="question-dropdown-label"
          value={selectedKey || ""}
          label={title}
          onChange={(e) => onSelect(e.target.value)}
        >
          {options.map((opt) => (
            <MenuItem key={opt.key} value={opt.key}>
              {opt.label}
            </MenuItem>
          ))}
        </Select>
      </FormControl>
    </Stack>
  );
}
