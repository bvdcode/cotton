import { spawn } from "node:child_process";
import { cp, mkdir, readdir, readFile, rm } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const clientRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const projectRoot = resolve(clientRoot, "../Cotton.Heic.Browser");
const publishRoot = join(projectRoot, "bin/browser-publish");
const publicRoot = join(clientRoot, "public/heic");

await rm(publishRoot, { recursive: true, force: true });

await new Promise((resolveBuild, rejectBuild) => {
  const processHandle = spawn(
    process.env.DOTNET_EXECUTABLE ?? "dotnet",
    [
      "publish",
      join(projectRoot, "Cotton.Heic.Browser.csproj"),
      "-c",
      "Release",
      "--output",
      publishRoot,
      "--nologo",
    ],
    { cwd: clientRoot, stdio: "inherit" },
  );
  processHandle.on("error", rejectBuild);
  processHandle.on("exit", (code) => {
    if (code === 0) {
      resolveBuild();
    } else {
      rejectBuild(
        new Error(`HEIC browser build failed with exit code ${code}.`),
      );
    }
  });
});

const frameworkRoot = join(publishRoot, "wwwroot/_framework");
const frameworkFiles = await readdir(frameworkRoot);
if (!frameworkFiles.includes("dotnet.js")) {
  throw new Error(
    "The HEIC browser build did not publish its runtime entry point.",
  );
}
const libraryAssets = JSON.parse(
  await readFile(join(projectRoot, "obj/project.assets.json"), "utf8"),
);
const heicPackage = Object.keys(libraryAssets.libraries).find((name) =>
  name.startsWith("Heic.NET/"),
);
if (!heicPackage) {
  throw new Error("The HEIC browser build did not restore Heic.NET.");
}

await mkdir(publicRoot, { recursive: true });
await rm(join(publicRoot, "_framework"), { recursive: true, force: true });
await cp(frameworkRoot, join(publicRoot, "_framework"), {
  recursive: true,
  filter: (source) => !source.endsWith(".br") && !source.endsWith(".gz"),
});
await cp(join(publishRoot, "wwwroot/Licenses"), join(publicRoot, "Licenses"), {
  recursive: true,
});
process.stdout.write(`Published ${heicPackage} browser decoder.\n`);
