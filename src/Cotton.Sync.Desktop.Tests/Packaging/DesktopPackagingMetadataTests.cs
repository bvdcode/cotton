// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Text.RegularExpressions;
using System.Xml.Linq;
using Cotton.Sync.Desktop.Platform;

namespace Cotton.Sync.Desktop.Tests.Packaging
{
    public class DesktopPackagingMetadataTests
    {
        [Test]
        public void DesktopProject_DefinesWindowsAndLinuxReleaseMetadata()
        {
            XDocument project = XDocument.Load(GetDesktopProjectPath());
            XElement propertyGroup = project.Root!.Elements("PropertyGroup").First();

            Assert.Multiple(() =>
            {
                Assert.That(GetProperty(propertyGroup, "UseAppHost"), Is.EqualTo("true"));
                Assert.That(GetProperty(propertyGroup, "Title"), Is.EqualTo("Cotton Sync"));
                Assert.That(GetProperty(propertyGroup, "Product"), Is.EqualTo("Cotton Sync"));
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
        public void DesktopProject_GeneratesChecksumsWithPublishRelativePaths()
        {
            XDocument project = XDocument.Load(GetDesktopProjectPath());
            XElement target = project.Root!
                .Elements("Target")
                .Single(static element => string.Equals(
                    element.Attribute("Name")?.Value,
                    "GeneratePublishChecksums",
                    StringComparison.Ordinal));

            Assert.Multiple(() =>
            {
                Assert.That(target.ToString(), Does.Contain("CottonPublishDir"));
                Assert.That(target.ToString(), Does.Contain("AssignTargetPath"));
                Assert.That(target.ToString(), Does.Contain("ManifestPath"));
                Assert.That(target.ToString(), Does.Contain("RootFolder=\"$(CottonPublishDir)\""));
                Assert.That(target.ToString(), Does.Contain("%(FileHash)  %(ManifestPath)"));
                Assert.That(target.ToString(), Does.Not.Contain("%(RecursiveDir)%(Filename)%(Extension)"));
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
                Assert.That(packageScript, Does.Not.Contain("rm -f \"$package_root/opt/cotton-sync/cotton-sync.desktop\""));
                Assert.That(packageScript, Does.Contain("checksums.sha256"));
                Assert.That(packageScript, Does.Contain("Package: cotton-sync-desktop"));
                Assert.That(packageScript, Does.Contain("cat > \"$package_root/DEBIAN/postrm\""));
                Assert.That(packageScript, Does.Contain("cleanup_autostart_file"));
                Assert.That(packageScript, Does.Contain("Name=Cotton Sync"));
                Assert.That(packageScript, Does.Contain("Exec=/opt/cotton-sync/Cotton.Sync.Desktop"));
                Assert.That(packageScript, Does.Contain("chmod 755 \"$package_root/DEBIAN/postrm\""));
                Assert.That(packageScript, Does.Contain("Architecture: amd64"));
                Assert.That(packageScript, Does.Contain("Depends: libnotify-bin, libsecret-tools"));
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
                Assert.That(smokeScript, Does.Contain("command -v xprop"));
                Assert.That(smokeScript, Does.Contain("command -v xwininfo"));
                Assert.That(smokeScript, Does.Not.Contain("command -v xwd"));
                Assert.That(smokeScript, Does.Contain("\"$app_executable\" --data-dir \"$data_dir\" \"$@\""));
                Assert.That(smokeScript, Does.Contain("xprop -id \"$window_id\" _NET_WM_PID"));
                Assert.That(smokeScript, Does.Contain("xwininfo -root -tree"));
                Assert.That(smokeScript, Does.Contain("Desktop app window was not found for process"));
                Assert.That(smokeScript, Does.Contain("get_window_size()"));
                Assert.That(smokeScript, Does.Contain("Could not detect desktop app window size."));
                Assert.That(smokeScript, Does.Contain("get_window_origin()"));
                Assert.That(smokeScript, Does.Contain("Could not detect desktop app window origin."));
                Assert.That(smokeScript, Does.Contain("resize_app_window_if_requested()"));
                Assert.That(smokeScript, Does.Contain("COTTON_SYNC_SCREENSHOT_WINDOW_SIZE"));
                Assert.That(smokeScript, Does.Contain("must use WIDTHxHEIGHT"));
                Assert.That(smokeScript, Does.Contain("wmctrl -ir \"$app_window_id\" -e"));
                Assert.That(smokeScript, Does.Contain("wmctrl -ia \"$app_window_id\""));
                Assert.That(smokeScript, Does.Contain("-f x11grab"));
                Assert.That(smokeScript, Does.Contain("-video_size \"$capture_size\""));
                Assert.That(smokeScript, Does.Contain("-i \"${DISPLAY}+${capture_origin}\""));
                Assert.That(smokeScript, Does.Contain("-frames:v 1"));
                Assert.That(smokeScript, Does.Contain("Desktop app exited during screenshot capture."));
                Assert.That(smokeScript, Does.Contain("TypeLoadException"));
                Assert.That(smokeScript, Does.Contain("Desktop app log contains runtime exception signatures."));
                Assert.That(smokeScript, Does.Contain("GUI screenshot was not created"));
                Assert.That(smokeScript, Does.Contain("ffprobe -v error"));
                Assert.That(smokeScript, Does.Contain("expected app window $capture_size"));
                Assert.That(smokeScript, Does.Contain("lavfi.signalstats.YMIN"));
                Assert.That(smokeScript, Does.Contain("GUI screenshot appears to be a single-color frame."));
                Assert.That(smokeScript, Does.Contain("Captured desktop GUI screenshot"));
            });
        }

        [Test]
        public void LinuxGuiScreenshotMatrixScript_CapturesDefaultVisualSmokeStates()
        {
            string smokeScript = File.ReadAllText(GetDesktopFilePath("Packaging/linux/smoke-gui-screenshot-matrix.sh"));

            Assert.Multiple(() =>
            {
                Assert.That(smokeScript, Does.Contain("Usage: smoke-gui-screenshot-matrix.sh <app-executable> <output-dir> [scenario...]"));
                Assert.That(smokeScript, Does.Contain("DISPLAY is required"));
                Assert.That(smokeScript, Does.Contain("set -- sign-in-error empty-dashboard add-folder dashboard folder-controls progress settings settings-diagnostics error conflict"));
                Assert.That(smokeScript, Does.Contain("smoke-gui-screenshot.sh"));
                Assert.That(smokeScript, Does.Contain("cotton-sync-desktop-linux-gui.png"));
                Assert.That(smokeScript, Does.Contain("cotton-sync-desktop-linux-${scenario}.png"));
                Assert.That(smokeScript, Does.Contain("--visual-smoke \"$scenario\""));
            });
        }

        [Test]
        public void LinuxDiagnosticsExportSmokeScript_VerifiesBundleMetadata()
        {
            string smokeScript = File.ReadAllText(GetDesktopFilePath("Packaging/linux/smoke-diagnostics-export.sh"));

            Assert.Multiple(() =>
            {
                Assert.That(smokeScript, Does.Contain("Usage: $0 <app-executable> <data-dir>"));
                Assert.That(smokeScript, Does.Contain("--export-diagnostics --data-dir"));
                Assert.That(smokeScript, Does.Contain("command -v timeout"));
                Assert.That(smokeScript, Does.Contain("COTTON_SYNC_DIAGNOSTICS_TIMEOUT"));
                Assert.That(smokeScript, Does.Contain("Diagnostics export timed out after"));
                Assert.That(smokeScript, Does.Contain("Diagnostics export exited with code"));
                Assert.That(smokeScript, Does.Contain("sed -n 's/^Bundle: //p'"));
                Assert.That(smokeScript, Does.Contain("Diagnostics bundle path was not reported."));
                Assert.That(smokeScript, Does.Contain("Diagnostics bundle was not created at $bundle_path."));
                Assert.That(smokeScript, Does.Contain("command -v python3"));
                Assert.That(smokeScript, Does.Contain("diagnostics.json"));
                Assert.That(smokeScript, Does.Contain("\"dataPaths\""));
                Assert.That(smokeScript, Does.Contain("\"sync-app.db\""));
                Assert.That(smokeScript, Does.Contain("\"sync-state.db\""));
                Assert.That(smokeScript, Does.Contain("\"tokens.json\""));
                Assert.That(smokeScript, Does.Contain("Verified diagnostics bundle metadata:"));
                Assert.That(smokeScript, Does.Contain("Exported diagnostics bundle:"));
            });
        }

        [Test]
        public void LinuxChecksumVerificationScript_VerifiesPublishedManifest()
        {
            string checksumScript = File.ReadAllText(GetDesktopFilePath("Packaging/linux/verify-checksums.sh"));

            Assert.Multiple(() =>
            {
                Assert.That(checksumScript, Does.Contain("Usage: verify-checksums.sh <publish-dir>"));
                Assert.That(checksumScript, Does.Contain("checksums.sha256"));
                Assert.That(checksumScript, Does.Contain("sha256sum -c checksums.sha256"));
                Assert.That(checksumScript, Does.Contain("Verified publish checksums"));
            });
        }

        [Test]
        public void WindowsDiagnosticsExportSmokeScript_VerifiesBundleMetadata()
        {
            string smokeScript = File.ReadAllText(GetDesktopFilePath("Packaging/windows/smoke-diagnostics-export.ps1"));

            Assert.Multiple(() =>
            {
                Assert.That(smokeScript, Does.Contain("[string]$AppExecutable"));
                Assert.That(smokeScript, Does.Contain("[string]$DataDirectory"));
                Assert.That(smokeScript, Does.Contain("-ArgumentList @(\"--export-diagnostics\", \"--data-dir\", $DataDirectory)"));
                Assert.That(smokeScript, Does.Contain("-RedirectStandardOutput $stdoutPath"));
                Assert.That(smokeScript, Does.Contain("-RedirectStandardError $stderrPath"));
                Assert.That(smokeScript, Does.Contain("Diagnostics bundle path was not reported."));
                Assert.That(smokeScript, Does.Contain("Diagnostics bundle was not created at $bundlePath."));
                Assert.That(smokeScript, Does.Contain("System.IO.Compression.ZipFile"));
                Assert.That(smokeScript, Does.Contain("diagnostics.json"));
                Assert.That(smokeScript, Does.Contain("ConvertFrom-Json"));
                Assert.That(smokeScript, Does.Contain("dataPaths"));
                Assert.That(smokeScript, Does.Contain("sync-app.db"));
                Assert.That(smokeScript, Does.Contain("sync-state.db"));
                Assert.That(smokeScript, Does.Contain("tokens.json"));
                Assert.That(smokeScript, Does.Contain("Verified diagnostics bundle metadata:"));
                Assert.That(smokeScript, Does.Contain("Exported diagnostics bundle:"));
            });
        }

        [Test]
        public void WindowsChecksumVerificationScript_VerifiesPublishedManifest()
        {
            string checksumScript = File.ReadAllText(GetDesktopFilePath("Packaging/windows/verify-checksums.ps1"));

            Assert.Multiple(() =>
            {
                Assert.That(checksumScript, Does.Contain("[string]$PublishDirectory"));
                Assert.That(checksumScript, Does.Contain("checksums.sha256"));
                Assert.That(checksumScript, Does.Contain("Get-FileHash -Algorithm SHA256"));
                Assert.That(checksumScript, Does.Contain("Checksum mismatch"));
                Assert.That(checksumScript, Does.Contain("No publish checksums were verified."));
            });
        }

        [Test]
        public void CiWorkflow_BuildsAndUploadsLinuxDebArtifact()
        {
            string workflow = GetDesktopWorkflow();

            Assert.Multiple(() =>
            {
                Assert.That(workflow, Does.Contain("Package desktop Linux x64 deb"));
                Assert.That(workflow, Does.Contain("src/Cotton.Sync.Desktop/Packaging/linux/package-deb.sh"));
                Assert.That(workflow, Does.Contain("cotton-sync-desktop-linux-x64-${{ steps.gitversion.outputs.SemVer }}.deb"));
                Assert.That(
                    Regex.Matches(workflow, "cotton-sync-desktop-linux-x64-\\$\\{\\{ steps\\.gitversion\\.outputs\\.SemVer \\}\\}\\.deb").Count,
                    Is.GreaterThanOrEqualTo(2));
            });
        }

        [Test]
        public void CiWorkflow_CapturesLinuxGuiScreenshot()
        {
            string workflow = GetDesktopWorkflow();

            Assert.Multiple(() =>
            {
                Assert.That(workflow, Does.Contain("ffmpeg gnome-keyring libnotify-bin libsecret-tools x11-apps x11-utils xauth xvfb"));
                Assert.That(workflow, Does.Contain("command -v xprop"));
                Assert.That(workflow, Does.Contain("command -v notify-send"));
                Assert.That(workflow, Does.Contain("command -v xwd"));
                Assert.That(workflow, Does.Contain("command -v xwininfo"));
                Assert.That(workflow, Does.Contain("Smoke desktop Linux GUI screenshot"));
                Assert.That(workflow, Does.Contain("xvfb-run -a -s \"-screen 0 1024x768x24\""));
                Assert.That(workflow, Does.Contain("Packaging/linux/smoke-gui-screenshot-matrix.sh"));
                Assert.That(workflow, Does.Contain("Upload desktop Linux GUI screenshot"));
                Assert.That(workflow, Does.Contain("name: desktop-linux-gui-screenshot"));
                Assert.That(workflow, Does.Contain("cotton-sync-desktop-linux-*.png"));
                Assert.That(workflow, Does.Contain("cotton-sync-desktop-linux-*.png.log"));
            });
        }

        [Test]
        public void CiWorkflow_SmokesLinuxPackageArtifacts()
        {
            string workflow = GetDesktopWorkflow();

            Assert.Multiple(() =>
            {
                Assert.That(workflow, Does.Contain("Smoke desktop Linux archive artifact"));
                Assert.That(workflow, Does.Contain("tar -xzf cotton-sync-desktop-linux-x64-${{ steps.gitversion.outputs.SemVer }}.tar.gz"));
                Assert.That(workflow, Does.Contain("self_test_timeout=120s"));
                Assert.That(workflow, Does.Contain("xvfb-run -a -s \"-screen 0 1024x768x24\""));
                Assert.That(workflow, Does.Contain("timeout \"$self_test_timeout\""));
                Assert.That(workflow, Does.Contain("\"$extract_dir/Cotton.Sync.Desktop\" --self-test --data-dir"));
                Assert.That(workflow, Does.Contain("Packaging/linux/verify-checksums.sh"));
                Assert.That(workflow, Does.Contain("Packaging/linux/smoke-diagnostics-export.sh"));
                Assert.That(workflow, Does.Contain("Smoke desktop Linux deb artifact"));
                Assert.That(workflow, Does.Contain("dpkg-deb -x cotton-sync-desktop-linux-x64-${{ steps.gitversion.outputs.SemVer }}.deb"));
                Assert.That(workflow, Does.Contain("test -f \"$extract_dir/usr/share/applications/cotton-sync.desktop\""));
                Assert.That(workflow, Does.Contain("test -f \"$extract_dir/usr/share/icons/hicolor/192x192/apps/cotton-sync.png\""));
                Assert.That(workflow, Does.Contain("test -L \"$extract_dir/usr/bin/cotton-sync\""));
                Assert.That(workflow, Does.Contain("\"$extract_dir/opt/cotton-sync\""));
                Assert.That(workflow, Does.Contain("\"$extract_dir/opt/cotton-sync/Cotton.Sync.Desktop\" --self-test --data-dir"));
            });
        }

        [Test]
        public void CiWorkflow_SmokesLinuxDebInstallAndUninstall()
        {
            string workflow = GetDesktopWorkflow();

            Assert.Multiple(() =>
            {
                Assert.That(workflow, Does.Contain("Smoke desktop Linux deb install"));
                Assert.That(workflow, Does.Contain("sudo dpkg -i cotton-sync-desktop-linux-x64-${{ steps.gitversion.outputs.SemVer }}.deb"));
                Assert.That(workflow, Does.Contain("sudo dpkg -r cotton-sync-desktop"));
                Assert.That(workflow, Does.Contain("test -x /opt/cotton-sync/Cotton.Sync.Desktop"));
                Assert.That(workflow, Does.Contain("test -L /usr/bin/cotton-sync"));
                Assert.That(workflow, Does.Contain("Packaging/linux/verify-checksums.sh /opt/cotton-sync"));
                Assert.That(workflow, Does.Contain("/opt/cotton-sync/Cotton.Sync.Desktop --self-test --data-dir"));
                Assert.That(workflow, Does.Contain("Packaging/linux/smoke-diagnostics-export.sh"));
                Assert.That(workflow, Does.Contain("test ! -e /opt/cotton-sync/Cotton.Sync.Desktop"));
                Assert.That(workflow, Does.Contain("test ! -e /usr/bin/cotton-sync"));
                Assert.That(workflow, Does.Contain("$HOME/.config/autostart/cotton-sync.desktop"));
                Assert.That(workflow, Does.Contain("Exec=/opt/cotton-sync/Cotton.Sync.Desktop"));
                Assert.That(workflow, Does.Contain("test ! -e \"$HOME/.config/autostart/cotton-sync.desktop\""));
            });
        }

        [Test]
        public void CiWorkflow_SmokesLinuxDebUpgrade()
        {
            string workflow = GetDesktopWorkflow();

            Assert.Multiple(() =>
            {
                Assert.That(workflow, Does.Contain("Smoke desktop Linux deb upgrade"));
                Assert.That(workflow, Does.Contain("cotton-sync-desktop-linux-x64-old.deb"));
                Assert.That(workflow, Does.Contain("0.0.1-ci-upgrade"));
                Assert.That(workflow, Does.Contain("sudo dpkg -i \"$old_deb\""));
                Assert.That(workflow, Does.Contain("sudo dpkg -i cotton-sync-desktop-linux-x64-${{ steps.gitversion.outputs.SemVer }}.deb"));
                Assert.That(workflow, Does.Contain("dpkg-query -W -f='${Version}' cotton-sync-desktop"));
                Assert.That(workflow, Does.Contain("Expected upgraded package version"));
                Assert.That(workflow, Does.Contain("Packaging/linux/verify-checksums.sh /opt/cotton-sync"));
                Assert.That(workflow, Does.Contain("/opt/cotton-sync/Cotton.Sync.Desktop --self-test --data-dir"));
                Assert.That(workflow, Does.Contain("Packaging/linux/smoke-diagnostics-export.sh"));
                Assert.That(workflow, Does.Contain("$HOME/.config/autostart/cotton-sync.desktop"));
                Assert.That(workflow, Does.Contain("Exec=/opt/cotton-sync/Cotton.Sync.Desktop"));
                Assert.That(workflow, Does.Contain("sudo dpkg -r cotton-sync-desktop"));
                Assert.That(workflow, Does.Contain("test ! -e /opt/cotton-sync/Cotton.Sync.Desktop"));
                Assert.That(workflow, Does.Contain("test ! -e /usr/bin/cotton-sync"));
                Assert.That(workflow, Does.Contain("test ! -e \"$HOME/.config/autostart/cotton-sync.desktop\""));
            });
        }

        [Test]
        public void CiWorkflow_RunsWindowsDesktopSmoke()
        {
            string workflow = GetDesktopWorkflow();

            Assert.Multiple(() =>
            {
                Assert.That(workflow, Does.Contain("windows:"));
                Assert.That(workflow, Does.Contain("Desktop Windows Package Smoke"));
                Assert.That(workflow, Does.Contain("runs-on: windows-latest"));
                Assert.That(workflow, Does.Contain("/p:PublishProfile=win-x64"));
                Assert.That(workflow, Does.Contain("-p:Version='${{ steps.gitversion.outputs.SemVer }}'"));
                Assert.That(workflow, Does.Contain("Packaging/windows/verify-associated-icon.ps1"));
                Assert.That(workflow, Does.Contain("-ExpectedIcon \"src/Cotton.Sync.Desktop/Assets/app.ico\""));
                Assert.That(workflow, Does.Contain("Cotton.Sync.Desktop.exe --self-test --data-dir"));
            });
        }

        [Test]
        public void WindowsAssociatedIconVerifier_ComparesPublishedExeWithAppIcon()
        {
            string iconScript = File.ReadAllText(GetDesktopFilePath("Packaging/windows/verify-associated-icon.ps1"));

            Assert.Multiple(() =>
            {
                Assert.That(iconScript, Does.Contain("[System.Drawing.Icon]::ExtractAssociatedIcon"));
                Assert.That(iconScript, Does.Contain("$expectedDesktopIcon = [System.Drawing.Icon]::ExtractAssociatedIcon($resolvedIcon)"));
                Assert.That(iconScript, Does.Contain("[System.Security.Cryptography.SHA256]::Create()"));
                Assert.That(iconScript, Does.Contain("ComputeHash($bytes)"));
                Assert.That(iconScript, Does.Contain("Desktop executable associated icon does not match"));
                Assert.That(iconScript, Does.Contain("Verified Windows associated icon"));
            });
        }

        [Test]
        public void WindowsShortcutAppIdVerifier_ReadsShortcutAppUserModelId()
        {
            string script = File.ReadAllText(GetDesktopFilePath("Packaging/windows/verify-shortcut-app-id.ps1"));

            Assert.Multiple(() =>
            {
                Assert.That(script, Does.Contain("New-Object -ComObject Shell.Application"));
                Assert.That(script, Does.Contain("System.AppUserModel.ID"));
                Assert.That(script, Does.Contain("ExpectedAppUserModelId"));
                Assert.That(script, Does.Contain("Windows shortcut AppUserModelID"));
                Assert.That(script, Does.Contain("Verified Windows shortcut AppUserModelID"));
            });
        }

        [Test]
        public void CiWorkflow_SmokesWindowsZipArchiveOnWindows()
        {
            string workflow = GetDesktopWorkflow();

            Assert.Multiple(() =>
            {
                Assert.That(workflow, Does.Contain("Setup Python"));
                Assert.That(workflow, Does.Contain("Smoke desktop Windows zip archive"));
                Assert.That(workflow, Does.Contain("Packaging/windows/package-zip.py"));
                Assert.That(workflow, Does.Contain("Packaging/windows/verify-checksums.ps1"));
                Assert.That(workflow, Does.Contain("Packaging/windows/verify-associated-icon.ps1"));
                Assert.That(workflow, Does.Contain("cotton-sync-desktop-win-x64-${{ steps.gitversion.outputs.SemVer }}.zip"));
                Assert.That(workflow, Does.Contain("Expand-Archive cotton-sync-desktop-win-x64-${{ steps.gitversion.outputs.SemVer }}.zip"));
                Assert.That(workflow, Does.Contain("Cotton.Sync.Desktop.exe\") --self-test --data-dir"));
                Assert.That(workflow, Does.Contain("Packaging/windows/smoke-diagnostics-export.ps1"));
                Assert.That(workflow, Does.Contain("-AppExecutable (Join-Path $extractDir \"Cotton.Sync.Desktop.exe\")"));
            });
        }

        [Test]
        public void CiWorkflow_UploadsWindowsZipPortableArtifact()
        {
            string workflow = GetDesktopWorkflow();
            string packageScript = File.ReadAllText(GetDesktopFilePath("Packaging/windows/package-zip.py"));

            Assert.Multiple(() =>
            {
                Assert.That(workflow, Does.Contain("Package desktop Windows x64 zip"));
                Assert.That(workflow, Does.Contain("src/Cotton.Sync.Desktop/Packaging/windows/package-zip.py"));
                Assert.That(workflow, Does.Contain("cotton-sync-desktop-win-x64-${{ steps.gitversion.outputs.SemVer }}.zip"));
                Assert.That(packageScript, Does.Contain("Cotton.Sync.Desktop.exe"));
                Assert.That(packageScript, Does.Contain("checksums.sha256"));
                Assert.That(packageScript, Does.Contain("ZipFile(output_zip, \"w\", ZIP_DEFLATED)"));
                Assert.That(packageScript, Does.Contain("path.relative_to(resolved_publish_dir).as_posix()"));
                Assert.That(
                    Regex.Matches(workflow, "cotton-sync-desktop-win-x64-\\$\\{\\{ steps\\.gitversion\\.outputs\\.SemVer \\}\\}\\.zip").Count,
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
                Assert.That(installerScript, Does.Contain("DefaultGroupName=Cotton Sync"));
                Assert.That(installerScript, Does.Contain("PrivilegesRequired=lowest"));
                Assert.That(installerScript, Does.Contain("ArchitecturesAllowed=x64compatible"));
                Assert.That(installerScript, Does.Contain("#define OutputBaseFilename \"cotton-sync-desktop-win-x64-setup\""));
                Assert.That(installerScript, Does.Contain("OutputBaseFilename={#OutputBaseFilename}"));
                Assert.That(installerScript, Does.Contain("SetupIconFile={#IconFile}"));
                Assert.That(installerScript, Does.Contain("UninstallDisplayIcon={app}\\Cotton.Sync.Desktop.exe"));
                Assert.That(installerScript, Does.Contain("#define AppMutexName \"CottonSyncDesktop_B671C18E_1E77_437C_AB9B_5C5C9D877E18\""));
                Assert.That(installerScript, Does.Contain("#define AppUserModelId \"" + DesktopAppIdentity.AppUserModelId + "\""));
                Assert.That(installerScript, Does.Contain("AppMutex={#AppMutexName}"));
                Assert.That(installerScript, Does.Contain("CloseApplications=force"));
                Assert.That(installerScript, Does.Contain("RestartApplications=no"));
                Assert.That(installerScript, Does.Contain("InitializeUninstall"));
                Assert.That(installerScript, Does.Contain("StopInstalledAppForSilentUninstall"));
                Assert.That(installerScript, Does.Contain("Get-CimInstance Win32_Process"));
                Assert.That(installerScript, Does.Contain("Stop-Process -Id $_.ProcessId -Force"));
                Assert.That(installerScript, Does.Contain("Wait-Process -Id $_.ProcessId -Timeout 5"));
                Assert.That(installerScript, Does.Contain("CheckForMutexes('{#AppMutexName}')"));
                Assert.That(installerScript, Does.Contain("Sleep(250)"));
                Assert.That(installerScript, Does.Contain("Silent uninstall app mutex released"));
                Assert.That(installerScript, Does.Contain("Source: \"{#SourceDir}\\*\""));
                Assert.That(installerScript, Does.Contain("recursesubdirs createallsubdirs"));
                Assert.That(installerScript, Does.Contain("Cotton.Sync.Desktop.exe"));
                Assert.That(installerScript, Does.Contain("Name: \"{group}\\Cotton Sync\""));
                Assert.That(installerScript, Does.Contain("Name: \"{group}\\Uninstall Cotton Sync\""));
                Assert.That(installerScript, Does.Contain("Filename: \"{uninstallexe}\""));
                Assert.That(installerScript, Does.Contain("IconFilename: \"{app}\\Cotton.Sync.Desktop.exe\""));
                Assert.That(Regex.Matches(installerScript, "AppUserModelID: \"\\{#AppUserModelId\\}\"").Count, Is.EqualTo(2));
                Assert.That(installerScript, Does.Contain("Create a desktop shortcut"));
                Assert.That(installerScript, Does.Contain("Root: HKCU; Subkey: \"Software\\Microsoft\\Windows\\CurrentVersion\\Run\""));
                Assert.That(installerScript, Does.Contain("ValueName: \"Cotton Sync\""));
                Assert.That(installerScript, Does.Contain("ValueData: \"\"\"{app}\\Cotton.Sync.Desktop.exe\"\" --start-minimized\""));
                Assert.That(installerScript, Does.Contain("Flags: uninsdeletevalue"));
                Assert.That(installerScript, Does.Contain("Flags: nowait postinstall skipifsilent"));
                Assert.That(installerScript, Does.Contain("CurUninstallStepChanged"));
                Assert.That(installerScript, Does.Contain("RegDeleteValue(HKCU, 'Software\\Microsoft\\Windows\\CurrentVersion\\Run', 'Cotton Sync')"));
            });
        }

        [Test]
        public void CiWorkflow_BuildsAndUploadsWindowsInstallerArtifact()
        {
            string workflow = GetDesktopWorkflow();

            Assert.Multiple(() =>
            {
                Assert.That(workflow, Does.Contain("Install Inno Setup"));
                Assert.That(workflow, Does.Contain("choco install innosetup"));
                Assert.That(workflow, Does.Contain("INNO_SETUP_COMPILER"));
                Assert.That(workflow, Does.Contain("Package desktop Windows installer"));
                Assert.That(workflow, Does.Contain("Packaging/windows/cotton-sync.iss"));
                Assert.That(workflow, Does.Contain("/DIconFile=$iconFile"));
                Assert.That(workflow, Does.Contain("/DAppVersion=${{ steps.gitversion.outputs.SemVer }}"));
                Assert.That(workflow, Does.Contain("/DOutputBaseFilename=cotton-sync-desktop-win-x64-${{ steps.gitversion.outputs.SemVer }}-setup"));
                Assert.That(workflow, Does.Contain("cotton-sync-desktop-win-x64-${{ steps.gitversion.outputs.SemVer }}-setup.exe"));
                Assert.That(workflow, Does.Contain("Upload desktop Windows installer artifact"));
                Assert.That(workflow, Does.Contain("name: desktop-windows-installer"));
            });
        }

        [Test]
        public void CiWorkflow_SmokesWindowsInstallerInstallAndUninstall()
        {
            string workflow = GetDesktopWorkflow();

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
                Assert.That(workflow, Does.Contain("[Environment]::GetFolderPath(\"Programs\")"));
                Assert.That(workflow, Does.Contain("Cotton Sync\\Cotton Sync.lnk"));
                Assert.That(workflow, Does.Contain("Cotton Sync\\Uninstall Cotton Sync.lnk"));
                Assert.That(workflow, Does.Contain("Installed Start Menu shortcut was not found."));
                Assert.That(workflow, Does.Contain("Installed Start Menu uninstall shortcut was not found."));
                Assert.That(workflow, Does.Contain("Packaging/windows/verify-shortcut-app-id.ps1"));
                Assert.That(workflow, Does.Contain("-ShortcutPath $startMenuShortcut"));
                Assert.That(workflow, Does.Contain("-ExpectedAppUserModelId \"Cotton.Sync.Desktop\""));
                Assert.That(workflow, Does.Contain("Cotton.Sync.Desktop.exe\""));
                Assert.That(workflow, Does.Contain("--self-test --data-dir"));
                Assert.That(workflow, Does.Contain("-PublishDirectory $installDir"));
                Assert.That(workflow, Does.Contain("-AppExecutable $installedExe"));
                Assert.That(workflow, Does.Contain("-ExpectedIcon \"src/Cotton.Sync.Desktop/Assets/app.ico\""));
                Assert.That(workflow, Does.Contain("Packaging/windows/smoke-diagnostics-export.ps1"));
                Assert.That(workflow, Does.Contain("unins000.exe"));
                Assert.That(workflow, Does.Contain("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run"));
                Assert.That(workflow, Does.Contain("Set-ItemProperty -Path $runKey -Name \"Cotton Sync\""));
                Assert.That(workflow, Does.Contain("Installed desktop executable remained after uninstall."));
                Assert.That(workflow, Does.Contain("Start Menu shortcut remained after uninstall."));
                Assert.That(workflow, Does.Contain("Start Menu uninstall shortcut remained after uninstall."));
                Assert.That(workflow, Does.Contain("Autostart registry value remained after uninstall."));
            });
        }

        [Test]
        public void CiWorkflow_SmokesWindowsInstallerUpgrade()
        {
            string workflow = GetDesktopWorkflow();

            Assert.Multiple(() =>
            {
                Assert.That(workflow, Does.Contain("Smoke desktop Windows installer upgrade"));
                Assert.That(workflow, Does.Contain("cotton-sync-old-installer"));
                Assert.That(workflow, Does.Contain("/DAppVersion=0.0.1-ci-upgrade"));
                Assert.That(workflow, Does.Contain("/DOutputBaseFilename=cotton-sync-desktop-win-x64-0.0.1-ci-upgrade-setup"));
                Assert.That(workflow, Does.Contain("Old Windows installer was not created."));
                Assert.That(workflow, Does.Contain("-FilePath $oldInstaller"));
                Assert.That(workflow, Does.Contain("$currentInstallerPath = \".\\cotton-sync-desktop-win-x64-${{ steps.gitversion.outputs.SemVer }}-setup.exe\""));
                Assert.That(workflow, Does.Contain("-FilePath $currentInstallerPath"));
                Assert.That(workflow, Does.Contain("Current Windows installer exited with code"));
                Assert.That(workflow, Does.Contain("Cotton Sync\\Cotton Sync.lnk"));
                Assert.That(workflow, Does.Contain("Cotton Sync\\Uninstall Cotton Sync.lnk"));
                Assert.That(workflow, Does.Contain("Upgraded Start Menu shortcut was not found."));
                Assert.That(workflow, Does.Contain("Upgraded Start Menu uninstall shortcut was not found."));
                Assert.That(workflow, Does.Contain("Packaging/windows/verify-shortcut-app-id.ps1"));
                Assert.That(workflow, Does.Contain("-ShortcutPath $startMenuShortcut"));
                Assert.That(workflow, Does.Contain("-ExpectedAppUserModelId \"Cotton.Sync.Desktop\""));
                Assert.That(workflow, Does.Contain("& $installedExe --self-test --data-dir"));
                Assert.That(workflow, Does.Contain("-PublishDirectory $installDir"));
                Assert.That(workflow, Does.Contain("-ExpectedIcon \"src/Cotton.Sync.Desktop/Assets/app.ico\""));
                Assert.That(workflow, Does.Contain("Packaging/windows/smoke-diagnostics-export.ps1"));
                Assert.That(workflow, Does.Contain("Windows uninstaller was not found after upgrade."));
                Assert.That(workflow, Does.Contain("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run"));
                Assert.That(workflow, Does.Contain("Set-ItemProperty -Path $runKey -Name \"Cotton Sync\""));
                Assert.That(workflow, Does.Contain("Upgraded desktop executable remained after uninstall."));
                Assert.That(workflow, Does.Contain("Upgraded Start Menu shortcut remained after uninstall."));
                Assert.That(workflow, Does.Contain("Upgraded Start Menu uninstall shortcut remained after uninstall."));
                Assert.That(workflow, Does.Contain("Upgraded autostart registry value remained after uninstall."));
            });
        }

        [Test]
        public void CiWorkflow_KeepsDesktopArtifactsOutOfDockerImageWorkflow()
        {
            string desktopWorkflow = GetDesktopWorkflow();
            string dockerWorkflow = GetDockerImageWorkflow();

            Assert.Multiple(() =>
            {
                Assert.That(desktopWorkflow, Does.Contain("cotton-sync-desktop-linux-x64-${{ steps.gitversion.outputs.SemVer }}.tar.gz"));
                Assert.That(desktopWorkflow, Does.Contain("cotton-sync-desktop-linux-x64-${{ steps.gitversion.outputs.SemVer }}.deb"));
                Assert.That(desktopWorkflow, Does.Contain("cotton-sync-desktop-win-x64-${{ steps.gitversion.outputs.SemVer }}.tar.gz"));
                Assert.That(desktopWorkflow, Does.Contain("cotton-sync-desktop-win-x64-${{ steps.gitversion.outputs.SemVer }}.zip"));
                Assert.That(desktopWorkflow, Does.Contain("cotton-sync-desktop-win-x64-${{ steps.gitversion.outputs.SemVer }}-setup.exe"));
                Assert.That(dockerWorkflow, Does.Not.Contain("cotton-sync-desktop-linux-x64"));
                Assert.That(dockerWorkflow, Does.Not.Contain("cotton-sync-desktop-win-x64"));
                Assert.That(dockerWorkflow, Does.Not.Contain("Cotton.Sync.Desktop/Packaging"));
            });
        }

        [Test]
        public void DesktopWorkflow_UploadsReleaseArtifactChecksums()
        {
            string workflow = GetDesktopWorkflow();

            Assert.Multiple(() =>
            {
                Assert.That(workflow, Does.Contain("release-checksums:"));
                Assert.That(workflow, Does.Contain("Desktop Release Artifact Checksums"));
                Assert.That(workflow, Does.Contain("actions/download-artifact@v4"));
                Assert.That(workflow, Does.Contain("name: desktop-linux-x64"));
                Assert.That(workflow, Does.Contain("name: desktop-win-x64"));
                Assert.That(workflow, Does.Contain("name: desktop-windows-installer"));
                Assert.That(workflow, Does.Contain("release-artifact-checksums.sha256"));
                Assert.That(workflow, Does.Contain("name: release-artifact-checksums"));
                Assert.That(workflow, Does.Contain("Expected 5 desktop release assets"));
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

        private static string GetDesktopWorkflow()
        {
            return File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "desktop-sync.yml")));
        }

        private static string GetDockerImageWorkflow()
        {
            return File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "docker-image.yml")));
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
}
