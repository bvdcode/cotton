// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Reflection;
using Cotton.Sync.App.Runners;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Shell;
using Cotton.Sync.Desktop.ViewModels;
using CoreSyncEngine = Cotton.Sync.SyncEngine;

namespace Cotton.Sync.Desktop.Tests.Shell
{
    public class DesktopUiBoundaryTests
    {
        [Test]
        public void ShellViewModel_UsesDesktopShellAbstractionsInsteadOfSyncEngine()
        {
            ConstructorInfo constructor = typeof(ShellViewModel)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single();
            Type[] parameterTypes = constructor.GetParameters()
                .Select(static parameter => parameter.ParameterType)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(parameterTypes, Does.Contain(typeof(IDesktopShellController)));
                Assert.That(parameterTypes, Does.Contain(typeof(ILocalFolderPicker)));
                Assert.That(parameterTypes, Does.Contain(typeof(IDesktopNotificationService)));
                Assert.That(parameterTypes, Does.Contain(typeof(IDesktopThemeService)));
                Assert.That(parameterTypes, Does.Not.Contain(typeof(CoreSyncEngine)));
                Assert.That(parameterTypes, Does.Not.Contain(typeof(SyncEnginePairWork)));
            });
        }

        [Test]
        public void UiShellTypes_DoNotStoreSyncEngineDependencies()
        {
            Type[] forbiddenTypes = [typeof(CoreSyncEngine), typeof(SyncEnginePairWork)];
            Type[] uiTypes = [typeof(MainWindow), typeof(ShellViewModel)];

            foreach (Type uiType in uiTypes)
            {
                Type[] dependencyTypes = uiType
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .Select(static field => field.FieldType)
                    .Concat(uiType
                        .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        .SelectMany(static constructor => constructor.GetParameters())
                        .Select(static parameter => parameter.ParameterType))
                    .ToArray();

                Assert.That(
                    dependencyTypes.Intersect(forbiddenTypes).ToArray(),
                    Is.Empty,
                    uiType.FullName + " must depend on desktop/app abstractions instead of sync engine internals.");
            }
        }
    }
}
