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
    internal class ProcessExePackageCommand
    {
        public ProcessExePackageCommand(IMessaging messaging, PackageFacade facade, Dictionary<string, WixBundlePayloadSymbol> payloadSymbols, Dictionary<string, SourceLineNumber> duplicateCacheIdDetector)
        {
            this.Messaging = messaging;
            this.AuthoredPayloads = payloadSymbols;
            this.Facade = facade;
            this.DuplicateCacheIdDetector = duplicateCacheIdDetector;
        }

        public IMessaging Messaging { get; }
        public PackageFacade Facade { get; }
        public Dictionary<string, WixBundlePayloadSymbol> AuthoredPayloads { get; }
        public Dictionary<string, SourceLineNumber> DuplicateCacheIdDetector { get; }

        /// <summary>
        /// Processes the Exe packages to add properties and payloads from the Exe packages.
        /// </summary>
        public void Execute()
        {
            var packagePayload = this.AuthoredPayloads[this.Facade.PackageSymbol.PayloadRef];

            if (String.IsNullOrEmpty(this.Facade.PackageSymbol.CacheId))
            {
                this.Facade.PackageSymbol.CacheId = packagePayload.Hash;
            }

            if (this.DuplicateCacheIdDetector.TryGetValue(this.Facade.PackageSymbol.CacheId, out var sourceLineNumber))
            {
                this.Messaging.Write(ErrorMessages.DuplicateCacheIds1(sourceLineNumber, this.Facade.PackageSymbol.CacheId));
                this.Messaging.Write(ErrorMessages.DuplicateCacheIds2(this.Facade.PackageSymbol.SourceLineNumbers, this.Facade.PackageSymbol.CacheId));
            }
            else
            {
                this.DuplicateCacheIdDetector.Add(this.Facade.PackageSymbol.CacheId, this.Facade.PackageSymbol.SourceLineNumbers);
            }

            this.Facade.PackageSymbol.Version = packagePayload.Version;
        }
    }
}
