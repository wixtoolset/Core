// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core
{
    using WixToolset.Data;
    using WixToolset.Extensibility.Data;

    internal class IncludedFile : IIncludedFile
    {
        public string Path { get; set; }

        public SourceLineNumber SourceLineNumbers { get; set; }
    }
}
