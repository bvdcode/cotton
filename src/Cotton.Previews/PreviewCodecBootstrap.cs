// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libheif;
using PhotoSauce.NativeCodecs.Libwebp;
using System.Runtime.InteropServices;

namespace Cotton.Previews
{
    internal static class PreviewCodecBootstrap
    {
        private const ulong MinHeifMaxMemoryBlockSizeBytes = 1024UL * 1024UL * 1024UL;
        private static int _initialized;

        public static void EnsureInitialized()
        {
            if (Interlocked.Exchange(ref _initialized, 1) == 1)
            {
                return;
            }

            CodecManager.Configure(codecs =>
            {
                codecs.UseLibheif();
                codecs.UseLibwebp();
            });

            EnsureHeifSecurityLimits();
        }

        private static void EnsureHeifSecurityLimits()
        {
            try
            {
                IntPtr limitsPtr = heif_get_global_security_limits();
                if (limitsPtr == IntPtr.Zero)
                {
                    return;
                }

                HeifSecurityLimits limits = Marshal.PtrToStructure<HeifSecurityLimits>(limitsPtr);
                if (limits.max_memory_block_size >= MinHeifMaxMemoryBlockSizeBytes)
                {
                    return;
                }

                limits.max_memory_block_size = MinHeifMaxMemoryBlockSizeBytes;
                Marshal.StructureToPtr(limits, limitsPtr, fDeleteOld: false);
            }
            catch (DllNotFoundException)
            {
                // libheif is optional and may be unavailable on unsupported runtimes.
            }
            catch (EntryPointNotFoundException)
            {
                // Older libheif builds may not expose security-limit APIs.
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HeifSecurityLimits
        {
            public byte version;
            public ulong max_image_size_pixels;
            public ulong max_number_of_tiles;
            public uint max_bayer_pattern_pixels;
            public uint max_items;
            public uint max_color_profile_size;
            public ulong max_memory_block_size;
            public uint max_components;
            public uint max_iloc_extents_per_item;
            public uint max_size_entity_group;
            public uint max_children_per_box;
        }

        // DllImport is intentionally used instead of LibraryImport here. The source-generated
        // interop path requires enabling unsafe code for the whole preview project, while this
        // optional libheif call is tiny, non-string marshalling-free, and isolated.
#pragma warning disable SYSLIB1054 // DllImport keeps this optional native probe simple without project-wide unsafe.
        [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern IntPtr heif_get_global_security_limits();
#pragma warning restore SYSLIB1054
    }
}
