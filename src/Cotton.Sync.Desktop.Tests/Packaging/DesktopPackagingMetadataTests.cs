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
    public void CiWorkflow_RunsWindowsDesktopSmokeBeforeRelease()
    {
        string workflow = File.ReadAllText(GetRepositoryFilePath(Path.Combine(".github", "workflows", "docker-image.yml")));

        Assert.Multiple(() =>
        {
            Assert.That(workflow, Does.Contain("desktop-windows-smoke:"));
            Assert.That(workflow, Does.Contain("runs-on: windows-latest"));
            Assert.That(workflow, Does.Contain("/p:PublishProfile=win-x64 -p:GeneratePublishChecksums=false"));
            Assert.That(workflow, Does.Contain("Cotton.Sync.Desktop.exe --self-test --data-dir"));
            Assert.That(workflow, Does.Contain("- desktop-windows-smoke"));
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
