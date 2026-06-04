// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

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
        string directory = TestContext.CurrentContext.TestDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            string candidate = Path.Combine(directory, "src", "Cotton.Sync.Desktop", relativePath);
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

        throw new FileNotFoundException(relativePath + " was not found from the test directory.");
    }
}
