/**
 * Language Selector Component
 *
 * Single Responsibility: Provides UI for selecting programming language
 * Open/Closed: Easy to add new languages via configuration
 */

import { Select, MenuItem, FormControl, InputLabel, Box } from "@mui/material";
import { Code } from "@mui/icons-material";

interface LanguageSelectorProps {
  currentLanguage: string;
  onLanguageChange: (language: string) => void;
  disabled?: boolean;
}

/**
 * Monaco Editor supported languages
 * Grouped for better UX
 */
const LANGUAGE_GROUPS = [
  {
    label: "Popular",
    languages: [
      { value: "javascript", label: "JavaScript" },
      { value: "typescript", label: "TypeScript" },
      { value: "python", label: "Python" },
      { value: "java", label: "Java" },
      { value: "csharp", label: "C#" },
      { value: "cpp", label: "C++" },
      { value: "go", label: "Go" },
      { value: "rust", label: "Rust" },
    ],
  },
  {
    label: "Web",
    languages: [
      { value: "html", label: "HTML" },
      { value: "css", label: "CSS" },
      { value: "scss", label: "SCSS" },
      { value: "less", label: "LESS" },
      { value: "json", label: "JSON" },
      { value: "xml", label: "XML" },
    ],
  },
  {
    label: "Scripting",
    languages: [
      { value: "shell", label: "Shell" },
      { value: "powershell", label: "PowerShell" },
      { value: "bat", label: "Batch" },
      { value: "ruby", label: "Ruby" },
      { value: "php", label: "PHP" },
      { value: "lua", label: "Lua" },
    ],
  },
  {
    label: "Functional",
    languages: [
      { value: "fsharp", label: "F#" },
      { value: "r", label: "R" },
    ],
  },
  {
    label: "Other",
    languages: [
      { value: "sql", label: "SQL" },
      { value: "yaml", label: "YAML" },
      { value: "dockerfile", label: "Dockerfile" },
      { value: "markdown", label: "Markdown" },
      { value: "diff", label: "Diff" },
      { value: "plaintext", label: "Plain Text" },
    ],
  },
] as const;

export const LanguageSelector: React.FC<LanguageSelectorProps> = ({
  currentLanguage,
  onLanguageChange,
  disabled = false,
}) => {
  return (
    <FormControl size="small" sx={{ minWidth: 150 }}>
      <InputLabel id="language-select-label">
        <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
          <Code fontSize="small" />
          <span>Language</span>
        </Box>
      </InputLabel>
      <Select
        labelId="language-select-label"
        value={currentLanguage}
        label="Language"
        onChange={(e) => onLanguageChange(e.target.value)}
        disabled={disabled}
        sx={{
          bgcolor: "background.paper",
          "& .MuiSelect-select": {
            py: 0.75,
          },
        }}
      >
        {LANGUAGE_GROUPS.map((group) => [
          <MenuItem
            key={`header-${group.label}`}
            disabled
            sx={{ fontWeight: "bold", fontSize: "0.75rem" }}
          >
            {group.label}
          </MenuItem>,
          ...group.languages.map((lang) => (
            <MenuItem key={lang.value} value={lang.value} sx={{ pl: 3 }}>
              {lang.label}
            </MenuItem>
          )),
        ])}
      </Select>
    </FormControl>
  );
};
