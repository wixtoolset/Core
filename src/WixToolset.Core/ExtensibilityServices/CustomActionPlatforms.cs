// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.ExtensibilityServices
{
    using System;

    /// <summary>
    /// Platforms supported by custom actions.
    /// </summary>
    [Flags]
    public enum CustomActionPlatforms
    {
        /// <summary>Not specified.</summary>
        None = 0,

        /// <summary>x86.</summary>
        X86 = 0x1,

        /// <summary>x64.</summary>
        X64 = 0x2,

        /// <summary>ia64.</summary>
        IA64 = 0x4,

        /// <summary>arm.</summary>
        ARM = 0x8,

        /// <summary>arm64.</summary>
        ARM64 = 0x10
    }
}
