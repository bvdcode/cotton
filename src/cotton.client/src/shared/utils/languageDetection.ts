export function detectMonacoLanguageFromFileName(fileName: string): string {
  const name = fileName.toLowerCase();
  const ext = name.split(".").pop() || "";

  // Special case: Dockerfile (no extension)
  if (name === "dockerfile" || name.startsWith("dockerfile.")) {
    return "dockerfile";
  }

  if (name === ".dockerignore") {
    return "ignore";
  }

  const languageMap: Record<string, string> = {
    // Rich IntelliSense languages
    ts: "typescript",
    tsx: "typescript",
    js: "javascript",
    jsx: "javascript",
    mjs: "javascript",
    cjs: "javascript",
    json: "json",
    jsonc: "json",
    html: "html",
    htm: "html",
    css: "css",
    less: "less",
    scss: "scss",
    sass: "scss",

    // Basic syntax colorization languages
    xml: "xml",
    svg: "xml",
    php: "php",
    phtml: "php",
    cs: "csharp",
    csx: "csharp",
    cpp: "cpp",
    cc: "cpp",
    cxx: "cpp",
    c: "c",
    h: "c",
    hpp: "cpp",
    razor: "razor",
    cshtml: "razor",
    md: "markdown",
    markdown: "markdown",
    diff: "diff",
    patch: "diff",
    java: "java",
    vb: "vb",
    coffee: "coffeescript",
    hbs: "handlebars",
    handlebars: "handlebars",
    bat: "bat",
    cmd: "bat",
    pug: "pug",
    jade: "pug",
    fs: "fsharp",
    fsi: "fsharp",
    fsx: "fsharp",
    fsscript: "fsharp",
    lua: "lua",
    ps1: "powershell",
    psm1: "powershell",
    psd1: "powershell",
    py: "python",
    pyw: "python",
    pyi: "python",
    rb: "ruby",
    rbw: "ruby",
    r: "r",
    m: "objective-c",
    mm: "objective-c",

    // Additional common languages (may have varying Monaco support)
    go: "go",
    rs: "rust",
    swift: "swift",
    kt: "kotlin",
    kts: "kotlin",
    sh: "shell",
    bash: "shell",
    zsh: "shell",
    yaml: "yaml",
    yml: "yaml",
    toml: "toml",
    ini: "ini",
    conf: "ini",
    cfg: "ini",
    sql: "sql",
    dockerfile: "dockerfile",
    vue: "html",
    svelte: "html",
  };

  return languageMap[ext] || "plaintext";
}
