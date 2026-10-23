// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    internal class ArchiveCrcState
    {
        private readonly Lock _lock = new();
        private ArchiveCrcCheckpoint _checkpoint;

        public ArchiveCrcCheckpoint Read()
        {
            lock (_lock)
            {
                return _checkpoint;
            }
        }

        public void Advance(long position, uint checksum)
        {
            lock (_lock)
            {
                if (position > _checkpoint.Position)
                {
                    _checkpoint = new(position, checksum);
                }
            }
        }
    }
}
