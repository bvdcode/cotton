// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Tests.Backends
{
    public partial class FileSystemStorageBackendTests
    {
        [Test]
        public async Task Overwrite_PublishesCompleteNewObject_WhileExistingReaderKeepsOldObject()
        {
            string key = NewUid();
            await _backend.WriteAsync(key, new MemoryStream([1, 2, 3]));
            await using Stream existingReader = await _backend.ReadAsync(key);
            await _backend.WriteAsync(key, new MemoryStream([4, 5]), overwrite: true);
            await using Stream newReader = await _backend.ReadAsync(key);
            using MemoryStream oldContent = new();
            using MemoryStream newContent = new();
            await existingReader.CopyToAsync(oldContent);
            await newReader.CopyToAsync(newContent);
            Assert.That(oldContent.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(newContent.ToArray(), Is.EqualTo(new byte[] { 4, 5 }));
        }

        [Test]
        public async Task Overwrite_FailedInputLeavesPreviousObjectIntact()
        {
            string key = NewUid();
            await _backend.WriteAsync(key, new MemoryStream([1, 2, 3]));
            MemoryStream disposed = new([4, 5]);
            await disposed.DisposeAsync();
            Assert.ThrowsAsync<ObjectDisposedException>(() => _backend.WriteAsync(key, disposed, overwrite: true));
            await using Stream reader = await _backend.ReadAsync(key);
            using MemoryStream content = new();
            await reader.CopyToAsync(content);
            Assert.That(content.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 }));
        }
    }
}
