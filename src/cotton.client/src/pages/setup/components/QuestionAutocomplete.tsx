import { Stack, Autocomplete, TextField } from "@mui/material";
import { QuestionHeader } from "./QuestionHeader";

type QuestionAutocompleteProps = {
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

export function QuestionAutocomplete({
  title,
  subtitle,
  linkUrl,
  linkAriaLabel,
  options,
  selectedKey,
  onSelect,
}: QuestionAutocompleteProps) {
  const selectedOption = options.find((opt) => opt.key === selectedKey) || null;

  return (
    <Stack spacing={3}>
      <QuestionHeader
        title={title}
        subtitle={subtitle}
        linkUrl={linkUrl}
        linkAriaLabel={linkAriaLabel}
      />
      <Autocomplete
        options={options}
        getOptionLabel={(option) => option.label}
        value={selectedOption}
        onChange={(_, newValue) => {
          if (newValue) {
            onSelect(newValue.key);
          }
        }}
        isOptionEqualToValue={(option, value) => option.key === value.key}
        renderInput={(params) => (
          <TextField
            {...params}
            label={title}
            placeholder={subtitle}
          />
        )}
        filterOptions={(options, state) => {
          const inputValue = state.inputValue.toLowerCase();
          return options.filter((option) =>
            option.label.toLowerCase().includes(inputValue)
          );
        }}
      />
    </Stack>
  );
}
