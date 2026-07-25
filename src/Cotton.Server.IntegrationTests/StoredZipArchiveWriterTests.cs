// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using Cotton.Server.Services;
using Microsoft.Extensions.Options;
using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class StoredZipArchiveWriterTests
{
    [Test]
    public async Task WriteAsync_QueuesStreamsUntilActiveBodyCompletes()
    {
        StoredZipArchiveWriter writer = CreateWriter(maxConcurrentStreams: 1);
        TaskCompletionSource firstOpened = CreateCompletionSource();
        TaskCompletionSource releaseFirst = CreateCompletionSource();
        TaskCompletionSource secondOpened = CreateCompletionSource();
        StoredZipSourceEntry firstEntry = CreateBlockingEntry(
            "first.bin",
            firstOpened,
            releaseFirst);
        StoredZipSourceEntry secondEntry = CreateObservedEntry("second.bin", secondOpened);
        using MemoryStream firstDestination = new();
        using MemoryStream secondDestination = new();

        Task firstWrite = writer.WriteAsync(
            firstDestination,
            [firstEntry],
            CancellationToken.None);
        await firstOpened.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Task secondWrite = writer.WriteAsync(
            secondDestination,
            [secondEntry],
            CancellationToken.None);
        Assert.That(secondOpened.Task.IsCompleted, Is.False);

        releaseFirst.SetResult();
        await firstWrite.WaitAsync(TimeSpan.FromSeconds(1));
        await secondOpened.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await secondWrite.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task WriteAsync_CancelledWaiterDoesNotConsumeStreamPermit()
    {
        StoredZipArchiveWriter writer = CreateWriter(maxConcurrentStreams: 1);
        TaskCompletionSource firstOpened = CreateCompletionSource();
        TaskCompletionSource releaseFirst = CreateCompletionSource();
        TaskCompletionSource cancelledOpened = CreateCompletionSource();
        TaskCompletionSource followerOpened = CreateCompletionSource();
        StoredZipSourceEntry firstEntry = CreateBlockingEntry(
            "first.bin",
            firstOpened,
            releaseFirst);
        StoredZipSourceEntry cancelledEntry = CreateObservedEntry(
            "cancelled.bin",
            cancelledOpened);
        StoredZipSourceEntry followerEntry = CreateObservedEntry(
            "follower.bin",
            followerOpened);
        using MemoryStream firstDestination = new();
        using MemoryStream cancelledDestination = new();
        using MemoryStream followerDestination = new();
        using CancellationTokenSource cancellation = new();

        Task firstWrite = writer.WriteAsync(
            firstDestination,
            [firstEntry],
            CancellationToken.None);
        await firstOpened.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Task cancelledWrite = writer.WriteAsync(
            cancelledDestination,
            [cancelledEntry],
            cancellation.Token);
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(
            async () => await cancelledWrite.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.That(cancelledOpened.Task.IsCompleted, Is.False);

        Task followerWrite = writer.WriteAsync(
            followerDestination,
            [followerEntry],
            CancellationToken.None);
        Assert.That(followerOpened.Task.IsCompleted, Is.False);

        releaseFirst.SetResult();
        await firstWrite.WaitAsync(TimeSpan.FromSeconds(1));
        await followerOpened.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await followerWrite.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task WriteAsync_ReleasesStreamPermitAfterFailure()
    {
        StoredZipArchiveWriter writer = CreateWriter(maxConcurrentStreams: 1);
        StoredZipSourceEntry failingEntry = new(
            "failing.bin",
            1,
            false,
            _ => throw new IOException("Synthetic archive source failure."));
        TaskCompletionSource followerOpened = CreateCompletionSource();
        StoredZipSourceEntry followerEntry = CreateObservedEntry(
            "follower.bin",
            followerOpened);
        using MemoryStream failingDestination = new();
        using MemoryStream followerDestination = new();

        Assert.ThrowsAsync<IOException>(
            async () => await writer.WriteAsync(
                failingDestination,
                [failingEntry],
                CancellationToken.None));

        await writer.WriteAsync(
            followerDestination,
            [followerEntry],
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(followerOpened.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task CentralDirectory_WritesZip64ExtraAfterFileName_WhenOnlyOffsetUsesZip64Sentinel()
    {
        const string path = "tiny.txt";
        byte[] pathBytes = Encoding.UTF8.GetBytes(path);
        long zip64Offset = uint.MaxValue;

        Type writerType = typeof(StoredZipArchiveWriter);
        Type planType = writerType.GetNestedType("ZipEntryPlan", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ZIP entry plan type was not found.");
        object plan = Activator.CreateInstance(
            planType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [path, pathBytes, 12L, false, false, zip64Offset],
            culture: null)
            ?? throw new InvalidOperationException("ZIP entry plan could not be created.");
        planType.GetProperty("CentralExtraLength")?.SetValue(plan, 12L);

        Type writtenType = writerType.GetNestedType("WrittenZipEntry", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Written ZIP entry type was not found.");
        object written = Activator.CreateInstance(
            writtenType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [plan, 0u],
            culture: null)
            ?? throw new InvalidOperationException("Written ZIP entry could not be created.");

        MethodInfo method = writerType.GetMethod("WriteCentralDirectoryEntryAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Central directory writer was not found.");
        using var destination = new MemoryStream();
        var task = (Task?)method.Invoke(null, [destination, written, CancellationToken.None]);
        Assert.That(task, Is.Not.Null);
        await task!;

        byte[] bytes = destination.ToArray();
        ushort nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28, 2));
        ushort extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(30, 2));

        Assert.Multiple(() =>
        {
            Assert.That(nameLength, Is.EqualTo(pathBytes.Length));
            Assert.That(extraLength, Is.EqualTo(12));
            Assert.That(bytes.AsSpan(46, pathBytes.Length).ToArray(), Is.EqualTo(pathBytes));
            Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(46 + pathBytes.Length, 2)), Is.EqualTo(0x0001));
            Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(48 + pathBytes.Length, 2)), Is.EqualTo(8));
            Assert.That(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(50 + pathBytes.Length, 8)), Is.EqualTo(zip64Offset));
        });
    }

    [Test]
    public void RequiresZip64CentralDirectoryMetadata_ReturnsFalse_WhenSizeAndOffsetFit()
    {
        bool requiresZip64 = StoredZipArchiveWriter.RequiresZip64CentralDirectoryMetadata(
            (long)uint.MaxValue - 1,
            (long)uint.MaxValue - 1);

        Assert.That(requiresZip64, Is.False);
    }

    [Test]
    public void RequiresZip64CentralDirectoryMetadata_ReturnsTrue_WhenOffsetUsesZip64Sentinel()
    {
        bool requiresZip64 = StoredZipArchiveWriter.RequiresZip64CentralDirectoryMetadata(
            1024,
            uint.MaxValue);

        Assert.That(requiresZip64, Is.True);
    }

    [Test]
    public void RequiresZip64CentralDirectoryMetadata_ReturnsTrue_WhenSizeUsesZip64Sentinel()
    {
        bool requiresZip64 = StoredZipArchiveWriter.RequiresZip64CentralDirectoryMetadata(
            uint.MaxValue,
            1024);

        Assert.That(requiresZip64, Is.True);
    }

    [Test]
    public void RequiresZip64CentralDirectoryMetadata_ReturnsTrue_WhenOnlyOffsetOverflows()
    {
        bool requiresZip64 = StoredZipArchiveWriter.RequiresZip64CentralDirectoryMetadata(
            1024,
            (long)uint.MaxValue + 1);

        Assert.That(requiresZip64, Is.True);
    }

    [Test]
    public void RequiresZip64CentralDirectoryMetadata_ReturnsTrue_WhenSizeOverflows()
    {
        bool requiresZip64 = StoredZipArchiveWriter.RequiresZip64CentralDirectoryMetadata(
            (long)uint.MaxValue + 1,
            1024);

        Assert.That(requiresZip64, Is.True);
    }

    private static StoredZipArchiveWriter CreateWriter(int maxConcurrentStreams)
    {
        ResourceConcurrencyOptions options = new()
        {
            HlsTranscodes = 1,
            ArchiveStreams = maxConcurrentStreams,
        };
        return new StoredZipArchiveWriter(Options.Create(options));
    }

    private static StoredZipSourceEntry CreateBlockingEntry(
        string path,
        TaskCompletionSource opened,
        TaskCompletionSource release)
    {
        return new StoredZipSourceEntry(
            path,
            1,
            false,
            _ => ValueTask.FromResult<Stream>(new BlockingReadStream(opened, release)));
    }

    private static StoredZipSourceEntry CreateObservedEntry(
        string path,
        TaskCompletionSource opened)
    {
        return new StoredZipSourceEntry(
            path,
            1,
            false,
            _ =>
            {
                opened.TrySetResult();
                return ValueTask.FromResult<Stream>(new MemoryStream([1]));
            });
    }

    private static TaskCompletionSource CreateCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class BlockingReadStream(
        TaskCompletionSource readStarted,
        TaskCompletionSource releaseRead) : Stream
    {
        private bool _completed;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_completed)
            {
                return 0;
            }

            readStarted.TrySetResult();
            await releaseRead.Task.WaitAsync(cancellationToken);
            buffer.Span[0] = 1;
            _completed = true;
            return 1;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
