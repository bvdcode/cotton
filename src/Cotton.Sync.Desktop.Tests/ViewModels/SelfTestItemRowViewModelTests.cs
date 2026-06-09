// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.ViewModels;

namespace Cotton.Sync.Desktop.Tests.ViewModels
{

    public sealed class SelfTestItemRowViewModelTests
    {
        [Test]
        public void ResultState_TracksPassAndFailure()
        {
            var row = new SelfTestItemRowViewModel();

            Assert.Multiple(() =>
            {
                Assert.That(row.ResultText, Is.EqualTo("Issue"));
                Assert.That(row.IsFailed, Is.True);
            });

            row.Passed = true;

            Assert.Multiple(() =>
            {
                Assert.That(row.ResultText, Is.EqualTo("OK"));
                Assert.That(row.IsFailed, Is.False);
            });
        }
    }
}
