// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    /// <summary>
    /// MIME types for Android package archives that Cotton can preview.
    /// </summary>
    public static class AndroidPackageContentTypes
    {
        /// <summary>Canonical MIME type for APK package archives.</summary>
        public const string Apk = "application/vnd.android.package-archive";
        /// <summary>Legacy APK MIME type emitted by some clients.</summary>
        public const string ApkLegacy = "application/x-android-package-archive";
        /// <summary>Canonical MIME type for Android App Bundle archives.</summary>
        public const string AndroidAppBundle = "application/vnd.android.bundle";
        /// <summary>Legacy Android App Bundle MIME type emitted by some clients.</summary>
        public const string AndroidAppBundleLegacy = "application/x-android-app-bundle";
        /// <summary>Canonical MIME type for APK set archives.</summary>
        public const string Apks = "application/vnd.android.apks";
        /// <summary>Legacy APK set MIME type emitted by some clients.</summary>
        public const string ApksLegacy = "application/x-android-apks";
        /// <summary>Canonical MIME type for XAPK package archives.</summary>
        public const string Xapk = "application/vnd.android.xapk";
        /// <summary>Legacy XAPK MIME type emitted by some clients.</summary>
        public const string XapkLegacy = "application/x-android-xapk";
        /// <summary>Canonical MIME type for APKMirror package archives.</summary>
        public const string Apkm = "application/vnd.android.apkm";
        /// <summary>Legacy APKMirror package MIME type emitted by some clients.</summary>
        public const string ApkmLegacy = "application/x-apkm";

        /// <summary>Gets every Android package MIME type that Cotton recognizes for previews.</summary>
        public static readonly string[] All =
        [
            Apk,
            ApkLegacy,
            AndroidAppBundle,
            AndroidAppBundleLegacy,
            Apks,
            ApksLegacy,
            Xapk,
            XapkLegacy,
            Apkm,
            ApkmLegacy,
        ];
    }
}
