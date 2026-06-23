const CODE_PREVIEW_EXTENSIONS = [
  "ts",
  "tsx",
  "js",
  "jsx",
  "mjs",
  "cjs",
  "json",
  "jsonc",
  "html",
  "htm",
  "css",
  "less",
  "scss",
  "sass",
  "xml",
  "php",
  "phtml",
  "cs",
  "csx",
  "cpp",
  "cc",
  "cxx",
  "c",
  "h",
  "hpp",
  "razor",
  "cshtml",
  "md",
  "markdown",
  "diff",
  "patch",
  "java",
  "vb",
  "coffee",
  "hbs",
  "handlebars",
  "bat",
  "cmd",
  "pug",
  "jade",
  "fs",
  "fsi",
  "fsx",
  "fsscript",
  "lua",
  "ps1",
  "psm1",
  "psd1",
  "py",
  "pyw",
  "pyi",
  "rb",
  "rbw",
  "r",
  "m",
  "mm",
  "go",
  "rs",
  "swift",
  "kt",
  "kts",
  "sh",
  "bash",
  "zsh",
  "yaml",
  "yml",
  "toml",
  "ini",
  "conf",
  "cfg",
  "sql",
  "vue",
  "svelte",
] as const;

const codePreviewExtensionSet = new Set<string>(CODE_PREVIEW_EXTENSIONS);

export const getCodeFileExtension = (fileName: string): string => {
  return fileName.toLowerCase().split(".").pop() || "";
};

export const isDockerfileName = (fileName: string): boolean => {
  const name = fileName.toLowerCase();
  return (
    name === "dockerfile" ||
    name.startsWith("dockerfile.") ||
    name === ".dockerignore"
  );
};

export const isCodePreviewFileName = (fileName: string): boolean => {
  return (
    isDockerfileName(fileName) ||
    codePreviewExtensionSet.has(getCodeFileExtension(fileName))
  );
};
