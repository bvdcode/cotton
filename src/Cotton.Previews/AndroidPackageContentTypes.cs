// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    public static class AndroidPackageContentTypes
    {
        public const string Apk = "application/vnd.android.package-archive";

        public const string ApkLegacy = "application/x-android-package-archive";

        public const string AndroidAppBundle = "application/vnd.android.bundle";

        public const string AndroidAppBundleLegacy = "application/x-android-app-bundle";

        public const string Apks = "application/vnd.android.apks";

        public const string ApksLegacy = "application/x-android-apks";

        public const string Xapk = "application/vnd.android.xapk";

        public const string XapkLegacy = "application/x-android-xapk";

        public const string Apkm = "application/vnd.android.apkm";

        public const string ApkmLegacy = "application/x-apkm";

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
