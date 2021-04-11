// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WixToolset.Data;
using WixToolset.Data.Symbols;
using WixToolset.Extensibility;
using WixToolset.Extensibility.Data;
using WixToolset.Extensibility.Services;

namespace WixToolset.Core.Burn
{
    internal class GatherWixVariablesCommand
    {
        /// <summary>
        /// Add all Wix variables to the variable cache.
        /// </summary>
        /// <param name="messaging"></param>
        /// <param name="intermediate">The Intermediate object representing compiled source document.</param>
        /// <param name="variableCache">The cached variable values used when resolving delayed fields.</param>
        public GatherWixVariablesCommand(IMessaging messaging, Intermediate intermediate, Dictionary<string, string> variableCache)
        {
            this.Messaging = messaging;
            this.Intermediate = intermediate;
            this.VariableCache = variableCache;
        }

        public IMessaging Messaging { get; }

        public Intermediate Intermediate { get; }

        public IDictionary<string, string> VariableCache { get; }

        public void Execute()
        {
            foreach (var sections in this.Intermediate.Sections)
            {
                foreach (var symbol in sections.Symbols)
                {
                    if (symbol is WixVariableSymbol wixVariableSymbol)
                    {
                        this.VariableCache[wixVariableSymbol.Id.Id] = wixVariableSymbol.Value;
                    }
                }
            }
        }
    }
}
