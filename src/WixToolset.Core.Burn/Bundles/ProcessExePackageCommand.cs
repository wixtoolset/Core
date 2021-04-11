// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.Burn.Bundles
{
    using System;
    using System.Collections.Generic;
    using WixToolset.Data;
    using WixToolset.Data.Symbols;
    using WixToolset.Extensibility.Services;

    /// <summary>
    /// Initializes package state from the Exe contents.
    /// </summary>
    internal class ProcessExePackageCommand : ProcessPackageCommand
    {
        public ProcessExePackageCommand(IServiceProvider serviceProvider, PackageFacade facade, Dictionary<string, WixBundlePayloadSymbol> payloadSymbols, Dictionary<string, SourceLineNumber> duplicateCacheIdDetector)
        : base(serviceProvider, facade, duplicateCacheIdDetector) => this.AuthoredPayloads = payloadSymbols;

        public Dictionary<string, WixBundlePayloadSymbol> AuthoredPayloads { get; }

        /// <summary>
        /// Processes the Exe packages to add properties and payloads from the Exe packages.
        /// </summary>
        public void Execute()
        {
            var packagePayload = this.AuthoredPayloads[this.Facade.PackageSymbol.PayloadRef];
            this.Facade.PackageSymbol.Version = packagePayload.Version;

            if (String.IsNullOrEmpty(this.Facade.PackageSymbol.CacheId))
            {
                this.Facade.PackageSymbol.CacheId = packagePayload.Hash;
            }

            this.CheckForDuplicateCacheIds();
        }
    }
}
