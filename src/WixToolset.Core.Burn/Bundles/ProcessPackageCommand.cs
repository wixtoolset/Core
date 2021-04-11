// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

using WixToolset.Extensibility.Services;

namespace WixToolset.Core.Burn.Bundles
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using WixToolset.Data;

    internal class ProcessPackageCommand
    {
        public ProcessPackageCommand(IServiceProvider serviceProvider, PackageFacade facade, Dictionary<string, SourceLineNumber> duplicateCacheIdDetector)
        {
            this.ServiceProvider = serviceProvider;
            this.Messaging = serviceProvider.GetService<IMessaging>();
            this.Facade = facade;
            this.DuplicateCacheIdDetector = duplicateCacheIdDetector;
        }

        public IServiceProvider ServiceProvider { get; }
        public IMessaging Messaging { get; }
        public PackageFacade Facade { get; }
        public Dictionary<string, SourceLineNumber> DuplicateCacheIdDetector { get; }

        public void CheckForDuplicateCacheIds()
        {
            if (this.DuplicateCacheIdDetector.TryGetValue(this.Facade.PackageSymbol.CacheId, out var sourceLineNumber))
            {
                this.Messaging.Write(BurnBackendErrors.DuplicateCacheIds1(sourceLineNumber, this.Facade.PackageSymbol.CacheId));
                this.Messaging.Write(BurnBackendErrors.DuplicateCacheIds2(this.Facade.PackageSymbol.SourceLineNumbers, this.Facade.PackageSymbol.CacheId));
            }
            else
            {
                this.DuplicateCacheIdDetector.Add(this.Facade.PackageSymbol.CacheId, this.Facade.PackageSymbol.SourceLineNumbers);
            }
        }
    }
}
