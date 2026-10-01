import { readdir, readFile } from "node:fs/promises";
import { extname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const sourceRoot = fileURLToPath(new URL("../src/", import.meta.url));
const allowedExtensions = new Set([".ts", ".tsx", ".css", ".scss"]);
const viewportUnit = /\b\d*\.?\d+(?:[sld]?v(?:h|w|i|b|min|max))\b/;
const violations = [];

async function inspectDirectory(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) {
      await inspectDirectory(path);
      continue;
    }
    if (!entry.isFile() || !allowedExtensions.has(extname(entry.name))) {
      continue;
    }
    const lines = (await readFile(path, "utf8")).split(/\r?\n/);
    for (const [index, line] of lines.entries()) {
      if (viewportUnit.test(line)) {
        violations.push(`${relative(sourceRoot, path)}:${index + 1}`);
      }
    }
  }
}

await inspectDirectory(sourceRoot);
if (violations.length > 0) {
  process.stderr.write(
    `Viewport units are not allowed in client layout:\n${violations.join("\n")}\n`,
  );
  process.exitCode = 1;
}
