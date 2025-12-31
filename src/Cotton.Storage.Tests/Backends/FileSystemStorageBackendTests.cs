// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Backends;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Storage.Tests.Backends
{
    [TestFixture]
    public class FileSystemStorageBackendTests
    {
        private FileSystemStorageBackend _backend = null!;
        private string _testBasePath = null!;
        private static string NewUid() => Guid.NewGuid().ToString("N")[..12];

        [SetUp]
        public void Setup()
        {
            var logger = new Mock<ILogger<FileSystemStorageBackend>>();
            _backend = new FileSystemStorageBackend(logger.Object);

            _testBasePath = Path.Combine(AppContext.BaseDirectory, "files");
            if (Directory.Exists(_testBasePath))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(_testBasePath, "*.*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    Directory.Delete(_testBasePath, true);
                }
                catch
                {
                    // Best-effort cleanup; leftover files should not fail test setup
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testBasePath))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(_testBasePath, "*.*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    Directory.Delete(_testBasePath, true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        [Test]
        public async Task FileSystemBackend_WriteAndRead_ReturnsOriginalData()
        {
            // Arrange
            string uid = NewUid();
            var originalData = Encoding.UTF8.GetBytes("Test content");

            // Act
            await _backend.WriteAsync(uid, new MemoryStream(originalData));
            await using var readStream = await _backend.ReadAsync(uid);

            // Assert
            using var result = new MemoryStream();
            await readStream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task FileSystemBackend_Delete_AfterWrite_ReturnsTrue()
        {
            // Arrange
            string uid = NewUid();
            var data = Encoding.UTF8.GetBytes("Test content");
            await _backend.WriteAsync(uid, new MemoryStream(data));

            // Act
            bool result = await _backend.DeleteAsync(uid);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task FileSystemBackend_Delete_AfterDelete_ThrowsFileNotFound()
        {
            // Arrange
            string uid = NewUid();
            var data = Encoding.UTF8.GetBytes("Test content");
            await _backend.WriteAsync(uid, new MemoryStream(data));
            await _backend.DeleteAsync(uid);

            // Act & Assert
            Assert.ThrowsAsync<FileNotFoundException>(() => _backend.ReadAsync(uid));
        }

        [Test]
        public async Task FileSystemBackend_Delete_NonExistent_ReturnsFalse()
        {
            // Arrange
            string uid = NewUid();

            // Act
            bool result = await _backend.DeleteAsync(uid);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task FileSystemBackend_Write_CreatesCorrectDirectoryStructure()
        {
            // Arrange
            string uid = "abcdef123456"; // keep deterministic for path assertion
            var data = Encoding.UTF8.GetBytes("Test content");

            // Act
            await _backend.WriteAsync(uid, new MemoryStream(data));

            // Assert
            string expectedPath = Path.Combine(_testBasePath, "ab", "cd", "ef123456.ctn");
            Assert.That(File.Exists(expectedPath), Is.True);
        }

        [Test]
        public async Task FileSystemBackend_Write_SetsReadOnlyAttribute()
        {
            // Arrange
            string uid = NewUid();
            var data = Encoding.UTF8.GetBytes("Test content");

            // Act
            await _backend.WriteAsync(uid, new MemoryStream(data));

            // Assert
            string filePath = Path.Combine(_testBasePath, uid[..2], uid.Substring(2, 2), string.Concat(uid.AsSpan(4), ".ctn"));
            var attributes = File.GetAttributes(filePath);
            Assert.That(attributes.HasFlag(FileAttributes.ReadOnly), Is.True);
        }

        [Test]
        public void FileSystemBackend_Write_DuplicateUid_ThrowsIOException()
        {
            // Arrange
            string uid = NewUid();
            var data1 = Encoding.UTF8.GetBytes("First");
            var data2 = Encoding.UTF8.GetBytes("Second");

            // Act & Assert
            Assert.DoesNotThrowAsync(() => _backend.WriteAsync(uid, new MemoryStream(data1)));
            Assert.ThrowsAsync<IOException>(() => _backend.WriteAsync(uid, new MemoryStream(data2)));
        }

        [Test]
        public async Task FileSystemBackend_Write_LargeFile_Success()
        {
            // Arrange
            string uid = NewUid();
            var data = new byte[10 * 1024 * 1024]; // 10 MB
            RandomNumberGenerator.Fill(data);

            // Act
            await _backend.WriteAsync(uid, new MemoryStream(data));
            await using var readStream = await _backend.ReadAsync(uid);

            // Assert
            var result = new MemoryStream();
            await readStream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public async Task FileSystemBackend_Read_NonExistent_ThrowsFileNotFoundException()
        {
            // Arrange
            string uid = "nonexistent";

            // Act & Assert
            Assert.ThrowsAsync<FileNotFoundException>(() => _backend.ReadAsync(uid));
        }

        [Test]
        public async Task FileSystemBackend_Write_InvalidUid_ThrowsArgumentException()
        {
            // Arrange
            string uid = "ab/cd";
            var data = Encoding.UTF8.GetBytes("Test");

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(() => _backend.WriteAsync(uid, new MemoryStream(data)));
        }

        [Test]
        public async Task FileSystemBackend_MultipleWrites_DifferentUids_Success()
        {
            // Arrange
            var uids = new[] { NewUid(), NewUid(), NewUid() };
            var dataMap = new Dictionary<string, byte[]>();

            foreach (var uid in uids)
            {
                var data = Encoding.UTF8.GetBytes($"Content for {uid}");
                dataMap[uid] = data;
                await _backend.WriteAsync(uid, new MemoryStream(data));
            }

            // Act & Assert
            foreach (var uid in uids)
            {
                await using var readStream = await _backend.ReadAsync(uid);
                var result = new MemoryStream();
                await readStream.CopyToAsync(result);
                Assert.That(result.ToArray(), Is.EqualTo(dataMap[uid]));
            }
        }

        [Test]
        public async Task FileSystemBackend_Write_StreamNotAtPositionZero_WritesFromCurrentPosition()
        {
            // Arrange
            string uid = NewUid();
            var data = Encoding.UTF8.GetBytes("0123456789");
            var stream = new MemoryStream(data) { Position = 5 };

            // Act
            await _backend.WriteAsync(uid, stream);
            await using var readStream = await _backend.ReadAsync(uid);

            // Assert
            var result = new MemoryStream();
            await readStream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public async Task FileSystemBackend_ParallelWrites_DifferentUids_AllSucceed()
        {
            // Arrange
            var tasks = new List<Task>();
            var uids = Enumerable.Range(0, 10).Select(_ => NewUid()).ToArray();

            // Act
            foreach (var uid in uids)
            {
                var data = Encoding.UTF8.GetBytes($"Content {uid}");
                tasks.Add(_backend.WriteAsync(uid, new MemoryStream(data)));
            }

            // Assert
            Assert.DoesNotThrowAsync(() => Task.WhenAll(tasks));

            foreach (var uid in uids)
            {
                Assert.DoesNotThrowAsync(async () => await _backend.ReadAsync(uid));
            }
        }
    }
}
