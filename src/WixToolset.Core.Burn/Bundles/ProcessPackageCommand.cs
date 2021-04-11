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
        public ProcessPackageCommand(IServiceProvider serviceProvider, PackageFacade facade)
        {
            this.ServiceProvider = serviceProvider;
            this.Messaging = serviceProvider.GetService<IMessaging>();
            this.Facade = facade;
        }

        public IServiceProvider ServiceProvider { get; }
        public IMessaging Messaging { get; }
        public PackageFacade Facade { get; }
    }
}
