// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cotton.Crypto.Internals.Pipelines
{
    internal class DecryptionResultWriter
    {
        private readonly Stream _output;
        private readonly BufferScope _scope;
        private readonly int _windowCap;
        private DecryptionResult[] _ring;
        private bool[] _filled;
        private long[] _slotIndex;
        private int _window;
        private long _nextToWrite;

        public DecryptionResultWriter(Stream output, BufferScope scope, int threads, int windowCap)
        {
            _output = output;
            _scope = scope;
            _windowCap = windowCap;
            const int minWindow = 4;
            _window = Math.Min(Math.Max(minWindow, threads * 4), _windowCap);
            _ring = new DecryptionResult[_window];
            _filled = new bool[_window];
            _slotIndex = new long[_window];
        }

        public long TotalWritten { get; private set; }

        public async Task AcceptAsync(DecryptionResult result, CancellationToken cancellationToken)
        {
            if (result.Index == _nextToWrite)
            {
                await WriteAndRecycleAsync(result, cancellationToken).ConfigureAwait(false);
                _nextToWrite++;
                await FlushReadyAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (result.Index < _nextToWrite)
            {
                _scope.Recycle(result.Data);
                throw new InvalidDataException(
                    $"Duplicate chunk index detected. Received {result.Index}, next expected {_nextToWrite}.");
            }

            try
            {
                EnsureCapacity(result.Index);
            }
            catch
            {
                _scope.Recycle(result.Data);
                throw;
            }

            int slot = (int)(result.Index % _window);
            if (_filled[slot])
            {
                _scope.Recycle(result.Data);
                throw new InvalidDataException(
                    $"Reorder buffer slot collision. Slot {slot} already filled for index {_slotIndex[slot]}, " +
                    $"tried to place {result.Index}.");
            }

            _ring[slot] = result;
            _slotIndex[slot] = result.Index;
            _filled[slot] = true;
        }

        public Task FlushAsync(CancellationToken cancellationToken)
        {
            return FlushReadyAsync(cancellationToken);
        }

        public void RecycleBuffered()
        {
            for (int i = 0; i < _filled.Length; i++)
            {
                if (!_filled[i])
                {
                    continue;
                }

                _scope.Recycle(_ring[i].Data);
                _ring[i] = default;
                _filled[i] = false;
            }
        }

        private void EnsureCapacity(long neededIndex)
        {
            if (neededIndex - _nextToWrite < _window)
            {
                return;
            }

            int newWindow = Math.Min(_window * 2, _windowCap);
            while (neededIndex - _nextToWrite >= newWindow && newWindow < _windowCap)
            {
                newWindow = Math.Min(newWindow * 2, _windowCap);
            }

            DecryptionResult[] newRing = new DecryptionResult[newWindow];
            bool[] newFilled = new bool[newWindow];
            long[] newSlotIndex = new long[newWindow];
            for (int i = 0; i < _window; i++)
            {
                if (!_filled[i])
                {
                    continue;
                }

                long index = _slotIndex[i];
                int newSlot = (int)(index % newWindow);
                newRing[newSlot] = _ring[i];
                newFilled[newSlot] = true;
                newSlotIndex[newSlot] = index;
            }

            _ring = newRing;
            _filled = newFilled;
            _slotIndex = newSlotIndex;
            _window = newWindow;
        }

        private async Task FlushReadyAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                int slot = (int)(_nextToWrite % _window);
                if (!_filled[slot] || _slotIndex[slot] != _nextToWrite)
                {
                    return;
                }

                DecryptionResult result = _ring[slot];
                _ring[slot] = default;
                _filled[slot] = false;
                await WriteAndRecycleAsync(result, cancellationToken).ConfigureAwait(false);
                _nextToWrite++;
            }
        }

        private async Task WriteAndRecycleAsync(
            DecryptionResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                await _output
                    .WriteAsync(result.Data.AsMemory(0, result.DataLength), cancellationToken)
                    .ConfigureAwait(false);
                TotalWritten += result.DataLength;
            }
            finally
            {
                _scope.Recycle(result.Data);
            }
        }
    }
}
