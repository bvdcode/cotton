// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Enums;

namespace Cotton.Storage.Pipelines
{
    public class StoragePipelineContext
    {
        public CompressionAlgorithm CompressionAlgorithm { get; set; }
    }
}
