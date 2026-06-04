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

    private static string? GetProperty(XElement propertyGroup, string name)
    {
        return propertyGroup.Element(name)?.Value;
    }

    private static string GetDesktopProjectPath()
    {
        string directory = TestContext.CurrentContext.TestDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            string candidate = Path.Combine(directory, "src", "Cotton.Sync.Desktop", "Cotton.Sync.Desktop.csproj");
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

        throw new FileNotFoundException("Cotton.Sync.Desktop.csproj was not found from the test directory.");
    }
}
