// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core
{
    using System.Collections.Generic;
    using System.Threading;
    using WixToolset.Data;
    using WixToolset.Extensibility;
    using WixToolset.Extensibility.Data;
    using WixToolset.Extensibility.Services;

    internal class PreprocessContext : IPreprocessContext
    {
        internal PreprocessContext(IWixToolsetServiceProvider serviceProvider)
        {
            this.ServiceProvider = serviceProvider;
        }

        public IWixToolsetServiceProvider ServiceProvider { get; }

        public IEnumerable<IPreprocessorExtension> Extensions { get; set; }

        public Platform Platform { get; set; }

        public IEnumerable<string> IncludeSearchPaths { get; set; }

        public string SourcePath { get; set; }

        public IDictionary<string, string> Variables { get; set; }

        public SourceLineNumber CurrentSourceLineNumber { get; set; }

        public CancellationToken CancellationToken { get; set; }
    }
}
