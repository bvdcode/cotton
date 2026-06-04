// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Cotton.Sync.Desktop.Tests.Packaging;

public sealed class DesktopPackagingMetadataTests
{
    [Test]
    public void DesktopProject_DefinesWindowsAndLinuxReleaseMetadata()
    {
        XDocument project = XDocument.Load(GetDesktopProjectPath());
        XElement propertyGroup = project.Root!.Elements("PropertyGroup").First();

        Assert.Multiple(() =>
        {
            Assert.That(GetProperty(propertyGroup, "UseAppHost"), Is.EqualTo("true"));
            Assert.That(GetProperty(propertyGroup, "ApplicationIcon"), Is.EqualTo("Assets/app.ico"));
            Assert.That(GetProperty(propertyGroup, "Win32Icon"), Is.EqualTo("Assets/app.ico"));
            Assert.That(
                GetProperty(propertyGroup, "RuntimeIdentifiers")?.Split(';'),
                Is.EquivalentTo(new[] { "win-x64", "linux-x64" }));
        });
    }

    [TestCase("win-x64")]
    [TestCase("linux-x64")]
    public void PublishProfile_DefinesSelfContainedPortableArtifact(string runtimeIdentifier)
    {
        XDocument profile = XDocument.Load(GetPublishProfilePath(runtimeIdentifier));
        XElement propertyGroup = profile.Root!.Elements("PropertyGroup").Single();

        Assert.Multiple(() =>
        {
            Assert.That(GetProperty(propertyGroup, "PublishProtocol"), Is.EqualTo("FileSystem"));
            Assert.That(GetProperty(propertyGroup, "Configuration"), Is.EqualTo("Release"));
            Assert.That(GetProperty(propertyGroup, "TargetFramework"), Is.EqualTo("net10.0"));
            Assert.That(GetProperty(propertyGroup, "RuntimeIdentifier"), Is.EqualTo(runtimeIdentifier));
            Assert.That(GetProperty(propertyGroup, "SelfContained"), Is.EqualTo("true"));
            Assert.That(GetProperty(propertyGroup, "UseAppHost"), Is.EqualTo("true"));
            Assert.That(GetProperty(propertyGroup, "PublishSingleFile"), Is.EqualTo("false"));
            Assert.That(GetProperty(propertyGroup, "PublishTrimmed"), Is.EqualTo("false"));
            Assert.That(GetProperty(propertyGroup, "PublishReadyToRun"), Is.EqualTo("false"));
            Assert.That(NormalizeProfilePath(GetProperty(propertyGroup, "PublishDir")), Does.EndWith("/publish/" + runtimeIdentifier + "/"));
        });
    }

    [Test]
    public void DesktopProject_CopiesLinuxDesktopEntryOnlyForLinuxPublish()
    {
        XDocument project = XDocument.Load(GetDesktopProjectPath());
        XElement content = project.Root!
            .Elements("ItemGroup")
            .Single(static itemGroup => string.Equals(
                itemGroup.Attribute("Condition")?.Value,
                "'$(RuntimeIdentifier)' == 'linux-x64'",
                StringComparison.Ordinal))
            .Elements("Content")
            .Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                content.Attribute("Include")?.Value,
                Is.EqualTo("Packaging/linux/cotton-sync.desktop"));
            Assert.That(content.Attribute("Link")?.Value, Is.EqualTo("cotton-sync.desktop"));
            Assert.That(content.Attribute("CopyToPublishDirectory")?.Value, Is.EqualTo("PreserveNewest"));
        });
    }

    [Test]
    public void DesktopProject_CleansPublishDirectoryBeforePublishing()
    {
        XDocument project = XDocument.Load(GetDesktopProjectPath());
        XElement target = project.Root!
            .Elements("Target")
            .Single(static element => string.Equals(
                element.Attribute("Name")?.Value,
                "CleanDesktopPublishDirectory",
                StringComparison.Ordinal));

        Assert.Multiple(() =>
        {
            Assert.That(target.Attribute("BeforeTargets")?.Value, Is.EqualTo("PrepareForPublish"));
            Assert.That(target.Attribute("Condition")?.Value, Does.Contain("Exists('$(PublishDir)')"));
            Assert.That(
                target.Elements("RemoveDir").Single().Attribute("Directories")?.Value,
                Is.EqualTo("$(PublishDir)"));
        });
    }

    [Test]
    public void LinuxDesktopEntry_DefinesLauncherMetadata()
    {
        string desktopEntry = File.ReadAllText(GetDesktopFilePath("Packaging/linux/cotton-sync.desktop"));

        Assert.Multiple(() =>
        {
            Assert.That(desktopEntry, Does.Contain("[Desktop Entry]"));
            Assert.That(desktopEntry, Does.Contain("Type=Application"));
            Assert.That(desktopEntry, Does.Contain("Name=Cotton Sync"));
            Assert.That(desktopEntry, Does.Contain("Exec=Cotton.Sync.Desktop"));
            Assert.That(desktopEntry, Does.Contain("TryExec=Cotton.Sync.Desktop"));
            Assert.That(desktopEntry, Does.Contain("Icon=cotton-sync"));
            Assert.That(desktopEntry, Does.Contain("Terminal=false"));
            Assert.That(desktopEntry, Does.Contain("Categories=Network;FileTransfer;"));
            Assert.That(desktopEntry, Does.Contain("StartupWMClass=Cotton.Sync.Desktop"));
        });
    }

    [Test]
    public void LinuxDebPackageScript_DefinesReleaseInstallLayout()
    {
        string packageScript = File.ReadAllText(GetDesktopFilePath("Packaging/linux/package-deb.sh"));

        Assert.Multiple(() =>
        {
            Assert.That(packageScript, Does.Contain("/opt/cotton-sync"));
            Assert.That(packageScript, Does.Contain("/usr/bin/cotton-sync"));
            Assert.That(packageScript, Does.Contain("/usr/share/applications/cotton-sync.desktop"));
            Assert.That(packageScript, Does.Contain("/usr/share/icons/hicolor/192x192/apps/cotton-sync.png"));
            Assert.That(packageScript, Does.Contain("rm -f \"$package_root/opt/cotton-sync/cotton-sync.desktop\""));
            Assert.That(packageScript, Does.Contain("checksums.sha256"));
            Assert.That(packageScript, Does.Contain("Package: cotton-sync-desktop"));
            Assert.That(packageScript, Does.Contain("Architecture: amd64"));
            Assert.That(packageScript, Does.Contain("Depends: libsecret-tools"));
            Assert.That(packageScript, Does.Contain("dpkg-deb --root-owner-group --build"));
        });
    }

    [Test]
    public void LinuxGuiScreenshotSmokeScript_CapturesPublishedAppWindow()
    {
        string smokeScript = File.ReadAllText(GetDesktopFilePath("Packaging/linux/smoke-gui-screenshot.sh"));

        Assert.Multiple(() =>
        {
            Assert.That(smokeScript, Does.Contain("[app-args...]"));
            Assert.That(smokeScript, Does.Contain("shift 2"));
            Assert.That(smokeScript, Does.Contain("DISPLAY is required"));
            Assert.That(smokeScript, Does.Contain("command -v ffmpeg"));
            Assert.That(smokeScript, Does.Contain("command -v ffprobe"));
            Assert.That(smokeScript, Does.Contain("\"$app_executable\" --data-dir \"$data_dir\" \"$@\""));
            Assert.That(smokeScript, Does.Contain("-f x11grab"));
            Assert.That(smokeScript, Does.Contain("-draw_mouse 0"));
            Assert.That(smokeScript, Does.Contain("-video_size \"$capture_size\""));
            Assert.That(smokeScript, Does.Contain("-frames:v 1"));
            Assert.That(smokeScript, Does.Contain("GUI screenshot was not created"));
            Assert.That(smokeScript, Does.Contain("ffprobe -v error"));
            Assert.That(smokeScript, Does.Contain("lavfi.signalstats.YMIN"));
            Assert.That(smokeScript, Does.Contain("GUI screenshot appears to be a single-color frame."));
            Assert.That(smokeScript, Does.Contain("Captured desktop GUI screenshot"));
        });
    }

    [Test]
    public void LinuxDiagnosticsExportSmokeScript_VerifiesBundlePath()
    {
        string smokeScript = File.ReadAllText(GetDesktopFilePath("Packaging/linux/smoke-diagnostics-export.sh"));

        Assert.Multiple(() =>
        {
            Assert.That(smokeScript, Does.Contain("Usage: $0 <app-executable> <data-dir>"));
            Assert.That(smokeScript, Does.Contain("--export-diagnostics --data-dir"));
            Assert.That(smokeScript, Does.Contain("sed -n 's/^Bundle: //p'"));
            Assert.That(smokeScript, Does.Contain("Diagnostics bundle path was not reported."));
            Assert.That(smokeScript, Does.Contain("Diagnostics bundle was not created at $bundle_path."));
            Assert.That(smokeScript, Does.Contain("Exported diagnostics bundle:"));
        });
    }

    [Test]
    public void WindowsDiagnosticsExportSmokeScript_VerifiesBundlePath()
    {
        string smokeScript = File.ReadAllText(GetDesktopFilePath("Packaging/windows/smoke-diagnostics-export.ps1"));

        Assert.Multiple(() =>
        {
            Assert.That(smokeScript, Does.Contain("[string]$AppExecutable"));
            Assert.That(smokeScript, Does.Contain("[string]$DataDirectory"));
            Assert.That(smokeScript, Does.Contain("--export-diagnostics --data-dir"));
            Assert.That(smokeScript, Does.Contain("Diagnostics bundle path was not reported."));
            Assert.That(smokeScript, Does.Contain("Diagnostics bundle was not created at $bundlePath."));
            Assert.That(smokeScript, Does.Contain("Exported diagnostics bundle:"));
        });
    }

    [Test]
    public void CiWorkflow_BuildsAndUploadsLinuxDebArtifact()
    {
        string workflow = File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "docker-image.yml")));

        Assert.Multiple(() =>
        {
            Assert.That(workflow, Does.Contain("Package desktop Linux x64 deb"));
            Assert.That(workflow, Does.Contain("src/Cotton.Sync.Desktop/Packaging/linux/package-deb.sh"));
            Assert.That(workflow, Does.Contain("cotton-sync-desktop-linux-x64.deb"));
            Assert.That(
                Regex.Matches(workflow, "cotton-sync-desktop-linux-x64\\.deb").Count,
                Is.GreaterThanOrEqualTo(2));
        });
    }

    [Test]
    public void CiWorkflow_CapturesLinuxGuiScreenshot()
    {
        string workflow = File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "docker-image.yml")));

        Assert.Multiple(() =>
        {
            Assert.That(workflow, Does.Contain("ffmpeg gnome-keyring libsecret-tools xauth xvfb"));
            Assert.That(workflow, Does.Contain("Smoke desktop Linux GUI screenshot"));
            Assert.That(workflow, Does.Contain("xvfb-run -a -s \"-screen 0 1024x768x24\""));
            Assert.That(workflow, Does.Contain("Packaging/linux/smoke-gui-screenshot.sh"));
            Assert.That(workflow, Does.Contain("cotton-sync-desktop-linux-gui.png"));
            Assert.That(workflow, Does.Contain("for scenario in dashboard settings error conflict; do"));
            Assert.That(workflow, Does.Contain("--visual-smoke \"$scenario\""));
            Assert.That(workflow, Does.Contain("Upload desktop Linux GUI screenshot"));
            Assert.That(workflow, Does.Contain("name: desktop-linux-gui-screenshot"));
            Assert.That(workflow, Does.Contain("cotton-sync-desktop-linux-*.png"));
            Assert.That(workflow, Does.Contain("cotton-sync-desktop-linux-*.png.log"));
        });
    }

    [Test]
    public void CiWorkflow_SmokesLinuxPackageArtifacts()
    {
        string workflow = File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "docker-image.yml")));

        Assert.Multiple(() =>
        {
            Assert.That(workflow, Does.Contain("Smoke desktop Linux archive artifact"));
            Assert.That(workflow, Does.Contain("tar -xzf cotton-sync-desktop-linux-x64.tar.gz"));
            Assert.That(workflow, Does.Contain("\"$extract_dir/Cotton.Sync.Desktop\" --self-test --data-dir"));
            Assert.That(workflow, Does.Contain("Packaging/linux/smoke-diagnostics-export.sh"));
            Assert.That(workflow, Does.Contain("Smoke desktop Linux deb artifact"));
            Assert.That(workflow, Does.Contain("dpkg-deb -x cotton-sync-desktop-linux-x64.deb"));
            Assert.That(workflow, Does.Contain("test -f \"$extract_dir/usr/share/applications/cotton-sync.desktop\""));
            Assert.That(workflow, Does.Contain("test -f \"$extract_dir/usr/share/icons/hicolor/192x192/apps/cotton-sync.png\""));
            Assert.That(workflow, Does.Contain("test -L \"$extract_dir/usr/bin/cotton-sync\""));
            Assert.That(workflow, Does.Contain("\"$extract_dir/opt/cotton-sync/Cotton.Sync.Desktop\" --self-test --data-dir"));
        });
    }

    [Test]
    public void CiWorkflow_SmokesLinuxDebInstallAndUninstall()
    {
        string workflow = File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "docker-image.yml")));

        Assert.Multiple(() =>
        {
            Assert.That(workflow, Does.Contain("Smoke desktop Linux deb install"));
            Assert.That(workflow, Does.Contain("sudo dpkg -i cotton-sync-desktop-linux-x64.deb"));
            Assert.That(workflow, Does.Contain("sudo dpkg -r cotton-sync-desktop"));
            Assert.That(workflow, Does.Contain("test -x /opt/cotton-sync/Cotton.Sync.Desktop"));
            Assert.That(workflow, Does.Contain("test -L /usr/bin/cotton-sync"));
            Assert.That(workflow, Does.Contain("/opt/cotton-sync/Cotton.Sync.Desktop --self-test --data-dir"));
            Assert.That(workflow, Does.Contain("Packaging/linux/smoke-diagnostics-export.sh"));
            Assert.That(workflow, Does.Contain("test ! -e /opt/cotton-sync/Cotton.Sync.Desktop"));
            Assert.That(workflow, Does.Contain("test ! -e /usr/bin/cotton-sync"));
        });
    }

    [Test]
    public void CiWorkflow_SmokesLinuxDebUpgrade()
    {
        string workflow = File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "docker-image.yml")));

        Assert.Multiple(() =>
        {
            Assert.That(workflow, Does.Contain("Smoke desktop Linux deb upgrade"));
            Assert.That(workflow, Does.Contain("cotton-sync-desktop-linux-x64-old.deb"));
            Assert.That(workflow, Does.Contain("0.0.1-ci-upgrade"));
            Assert.That(workflow, Does.Contain("sudo dpkg -i \"$old_deb\""));
            Assert.That(workflow, Does.Contain("sudo dpkg -i cotton-sync-desktop-linux-x64.deb"));
            Assert.That(workflow, Does.Contain("dpkg-query -W -f='${Version}' cotton-sync-desktop"));
            Assert.That(workflow, Does.Contain("Expected upgraded package version"));
            Assert.That(workflow, Does.Contain("/opt/cotton-sync/Cotton.Sync.Desktop --self-test --data-dir"));
            Assert.That(workflow, Does.Contain("Packaging/linux/smoke-diagnostics-export.sh"));
            Assert.That(workflow, Does.Contain("sudo dpkg -r cotton-sync-desktop"));
        });
    }

    [Test]
    public void CiWorkflow_RunsWindowsDesktopSmokeBeforeRelease()
    {
        string workflow = File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "docker-image.yml")));

        Assert.Multiple(() =>
        {
            Assert.That(workflow, Does.Contain("desktop-windows-smoke:"));
            Assert.That(workflow, Does.Contain("runs-on: windows-latest"));
            Assert.That(workflow, Does.Contain("needs: build"));
            Assert.That(workflow, Does.Contain("/p:PublishProfile=win-x64"));
            Assert.That(workflow, Does.Contain("-p:Version='${{ needs.build.outputs.Version }}'"));
            Assert.That(workflow, Does.Contain("Cotton.Sync.Desktop.exe --self-test --data-dir"));
            Assert.That(workflow, Does.Contain("- desktop-windows-smoke"));
        });
    }

    [Test]
    public void CiWorkflow_SmokesWindowsZipArchiveOnWindows()
    {
        string workflow = File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "docker-image.yml")));

        Assert.Multiple(() =>
        {
            Assert.That(workflow, Does.Contain("Setup Python"));
            Assert.That(workflow, Does.Contain("Smoke desktop Windows zip archive"));
            Assert.That(workflow, Does.Contain("Packaging/windows/package-zip.py"));
            Assert.That(workflow, Does.Contain("cotton-sync-desktop-win-x64-smoke.zip"));
            Assert.That(workflow, Does.Contain("Expand-Archive cotton-sync-desktop-win-x64-smoke.zip"));
            Assert.That(workflow, Does.Contain("Cotton.Sync.Desktop.exe\") --self-test --data-dir"));
            Assert.That(workflow, Does.Contain("Packaging/windows/smoke-diagnostics-export.ps1"));
            Assert.That(workflow, Does.Contain("-AppExecutable (Join-Path $extractDir \"Cotton.Sync.Desktop.exe\")"));
        });
    }

    [Test]
    public void CiWorkflow_UploadsWindowsZipPortableArtifact()
    {
        string workflow = File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "docker-image.yml")));
        string packageScript = File.ReadAllText(GetDesktopFilePath("Packaging/windows/package-zip.py"));

        Assert.Multiple(() =>
        {
            Assert.That(workflow, Does.Contain("Package desktop Windows x64 zip"));
            Assert.That(workflow, Does.Contain("src/Cotton.Sync.Desktop/Packaging/windows/package-zip.py"));
            Assert.That(workflow, Does.Contain("cotton-sync-desktop-win-x64.zip"));
            Assert.That(packageScript, Does.Contain("Cotton.Sync.Desktop.exe"));
            Assert.That(packageScript, Does.Contain("checksums.sha256"));
            Assert.That(packageScript, Does.Contain("ZipFile(output_zip, \"w\", ZIP_DEFLATED)"));
            Assert.That(packageScript, Does.Contain("path.relative_to(resolved_publish_dir).as_posix()"));
            Assert.That(
                Regex.Matches(workflow, "cotton-sync-desktop-win-x64\\.zip").Count,
                Is.GreaterThanOrEqualTo(2));
        });
    }

    [Test]
    public void WindowsInstallerScript_DefinesReleaseInstallLayout()
    {
        string installerScript = File.ReadAllText(GetDesktopFilePath("Packaging/windows/cotton-sync.iss"));

        Assert.Multiple(() =>
        {
            Assert.That(installerScript, Does.Contain("AppName=Cotton Sync"));
            Assert.That(installerScript, Does.Contain("DefaultDirName={localappdata}\\Programs\\Cotton Sync"));
            Assert.That(installerScript, Does.Contain("PrivilegesRequired=lowest"));
            Assert.That(installerScript, Does.Contain("ArchitecturesAllowed=x64compatible"));
            Assert.That(installerScript, Does.Contain("OutputBaseFilename=cotton-sync-desktop-win-x64-setup"));
            Assert.That(installerScript, Does.Contain("SetupIconFile={#IconFile}"));
            Assert.That(installerScript, Does.Contain("Source: \"{#SourceDir}\\*\""));
            Assert.That(installerScript, Does.Contain("recursesubdirs createallsubdirs"));
            Assert.That(installerScript, Does.Contain("Cotton.Sync.Desktop.exe"));
            Assert.That(installerScript, Does.Contain("IconFilename: \"{app}\\Cotton.Sync.Desktop.exe\""));
            Assert.That(installerScript, Does.Contain("Create a desktop shortcut"));
            Assert.That(installerScript, Does.Contain("Flags: nowait postinstall skipifsilent"));
        });
    }

    [Test]
    public void CiWorkflow_BuildsAndUploadsWindowsInstallerArtifact()
    {
        string workflow = File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "docker-image.yml")));

        Assert.Multiple(() =>
        {
            Assert.That(workflow, Does.Contain("Install Inno Setup"));
            Assert.That(workflow, Does.Contain("choco install innosetup"));
            Assert.That(workflow, Does.Contain("INNO_SETUP_COMPILER"));
            Assert.That(workflow, Does.Contain("Package desktop Windows installer"));
            Assert.That(workflow, Does.Contain("Packaging/windows/cotton-sync.iss"));
            Assert.That(workflow, Does.Contain("/DIconFile=$iconFile"));
            Assert.That(workflow, Does.Contain("cotton-sync-desktop-win-x64-setup.exe"));
            Assert.That(workflow, Does.Contain("Upload desktop Windows installer artifact"));
            Assert.That(workflow, Does.Contain("name: desktop-windows-installer"));
            Assert.That(workflow, Does.Contain("Download desktop Windows installer artifact"));
        });
    }

    [Test]
    public void CiWorkflow_SmokesWindowsInstallerInstallAndUninstall()
    {
        string workflow = File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "docker-image.yml")));

        Assert.Multiple(() =>
        {
            Assert.That(workflow, Does.Contain("Smoke desktop Windows installer"));
            Assert.That(workflow, Does.Contain("cotton-sync-installed"));
            Assert.That(workflow, Does.Contain("cotton-sync-installer-data"));
            Assert.That(workflow, Does.Contain("/VERYSILENT"));
            Assert.That(workflow, Does.Contain("/SUPPRESSMSGBOXES"));
            Assert.That(workflow, Does.Contain("/NORESTART"));
            Assert.That(workflow, Does.Contain("/TASKS="));
            Assert.That(workflow, Does.Contain("/DIR=$installDir"));
            Assert.That(workflow, Does.Contain("Cotton.Sync.Desktop.exe\""));
            Assert.That(workflow, Does.Contain("--self-test --data-dir"));
            Assert.That(workflow, Does.Contain("Packaging/windows/smoke-diagnostics-export.ps1"));
            Assert.That(workflow, Does.Contain("-AppExecutable $installedExe"));
            Assert.That(workflow, Does.Contain("unins000.exe"));
            Assert.That(workflow, Does.Contain("Installed desktop executable remained after uninstall."));
        });
    }

    [Test]
    public void CiWorkflow_SmokesWindowsInstallerUpgrade()
    {
        string workflow = File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "docker-image.yml")));

        Assert.Multiple(() =>
        {
            Assert.That(workflow, Does.Contain("Smoke desktop Windows installer upgrade"));
            Assert.That(workflow, Does.Contain("cotton-sync-old-installer"));
            Assert.That(workflow, Does.Contain("/DAppVersion=0.0.1-ci-upgrade"));
            Assert.That(workflow, Does.Contain("Old Windows installer was not created."));
            Assert.That(workflow, Does.Contain("-FilePath $oldInstaller"));
            Assert.That(workflow, Does.Contain("-FilePath \".\\cotton-sync-desktop-win-x64-setup.exe\""));
            Assert.That(workflow, Does.Contain("Current Windows installer exited with code"));
            Assert.That(workflow, Does.Contain("& $installedExe --self-test --data-dir"));
            Assert.That(workflow, Does.Contain("Packaging/windows/smoke-diagnostics-export.ps1"));
            Assert.That(workflow, Does.Contain("Windows uninstaller was not found after upgrade."));
            Assert.That(workflow, Does.Contain("Upgraded desktop executable remained after uninstall."));
        });
    }

    [Test]
    public void CiWorkflow_GeneratesReleaseArtifactChecksums()
    {
        string workflow = File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "docker-image.yml")));

        Assert.Multiple(() =>
        {
            Assert.That(workflow, Does.Contain("Generate release artifact checksums"));
            Assert.That(workflow, Does.Contain("release-artifact-checksums.sha256"));
            Assert.That(workflow, Does.Contain("find nupkg release-artifacts"));
            Assert.That(workflow, Does.Contain("! -name '*.sha256'"));
            Assert.That(workflow, Does.Contain("xargs -0 sha256sum"));
            Assert.That(workflow, Does.Contain("cotton-sync-desktop-linux-x64.tar.gz"));
            Assert.That(workflow, Does.Contain("cotton-sync-desktop-linux-x64.deb"));
            Assert.That(workflow, Does.Contain("cotton-sync-desktop-win-x64.tar.gz"));
            Assert.That(workflow, Does.Contain("cotton-sync-desktop-win-x64.zip"));
            Assert.That(workflow, Does.Contain("cotton-sync-desktop-win-x64-setup.exe"));
        });
    }

    private static string? GetProperty(XElement propertyGroup, string name)
    {
        return propertyGroup.Element(name)?.Value;
    }

    private static string NormalizeProfilePath(string? value)
    {
        return (value ?? string.Empty).Replace('\\', '/');
    }

    private static string GetDesktopProjectPath()
    {
        return GetDesktopFilePath("Cotton.Sync.Desktop.csproj");
    }

    private static string GetPublishProfilePath(string runtimeIdentifier)
    {
        return GetDesktopFilePath(Path.Combine("Properties", "PublishProfiles", runtimeIdentifier + ".pubxml"));
    }

    private static string GetDesktopFilePath(string relativePath)
    {
        string? path = TryGetRepositoryFilePath(Path.Combine("src", "Cotton.Sync.Desktop", relativePath));
        if (path is not null)
        {
            return path;
        }

        throw new FileNotFoundException(relativePath + " was not found from the test directory.");
    }

    private static string GetRepositoryFilePath(string relativePath)
    {
        string? path = TryGetRepositoryFilePath(relativePath);
        if (path is not null)
        {
            return path;
        }

        throw new FileNotFoundException(relativePath + " was not found from the test directory.");
    }

    private static string? TryGetRepositoryFilePath(string relativePath)
    {
        string directory = TestContext.CurrentContext.TestDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            string candidate = Path.Combine(directory, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            string? parent = Directory.GetParent(directory)?.FullName;
            if (parent == directory)
            {
                break;
            }

            directory = parent ?? string.Empty;
        }

        return null;
    }
}
