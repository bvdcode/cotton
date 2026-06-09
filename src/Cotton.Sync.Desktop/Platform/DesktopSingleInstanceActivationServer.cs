// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.IO.Pipes;

namespace Cotton.Sync.Desktop.Platform
{
    internal sealed class DesktopSingleInstanceActivationServer : IDisposable
    {
        private readonly Action _showWindow;
        private readonly CancellationTokenSource _shutdown = new();
        private readonly Task _listenTask;
        private readonly string _pipeName;
        private bool _disposed;

        private DesktopSingleInstanceActivationServer(string pipeName, Action showWindow)
        {
            _pipeName = pipeName;
            _showWindow = showWindow;
            _listenTask = ListenAsync(_shutdown.Token);
        }

        public static DesktopSingleInstanceActivationServer Start(string pipeName, Action showWindow)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
            ArgumentNullException.ThrowIfNull(showWindow);
            return new DesktopSingleInstanceActivationServer(pipeName, showWindow);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _shutdown.Cancel();
            try
            {
                _listenTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                Trace.TraceInformation("Cotton Sync single-instance activation listener stopped.");
            }
            finally
            {
                _shutdown.Dispose();
                _disposed = true;
            }
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await using NamedPipeServerStream pipe = CreatePipe();
                    await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                    using var reader = new StreamReader(pipe);
                    string? command = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (DesktopSingleInstanceActivation.IsShowCommand(command))
                    {
                        ShowWindow();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception) when (exception is IOException or ObjectDisposedException)
                {
                    Trace.TraceWarning("Cotton Sync single-instance activation listener failed: {0}", exception.Message);
                }
            }
        }

        private NamedPipeServerStream CreatePipe()
        {
            return new NamedPipeServerStream(
                _pipeName,
                PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }

        private void ShowWindow()
        {
            try
            {
                _showWindow();
            }
            catch (Exception exception)
            {
                Trace.TraceError("Cotton Sync single-instance activation action failed: {0}", exception);
            }
        }
    }
}
