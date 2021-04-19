// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.Burn
{
    using System.Collections.Generic;
    using System.Linq;
    using WixToolset.Data;
    using WixToolset.Data.Symbols;
    using WixToolset.Extensibility.Services;

    internal class GatherWixVariablesCommand
    {
        /// <summary>
        /// Add values for all Wix variables to the variable cache.
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
        public Dictionary<string, string> VariableCache { get; }

        public void Execute()
        {
            if (null != this.VariableCache)
            {
                var wixSymbols = this.Intermediate.Sections.Single().Symbols.OfType<WixVariableSymbol>().ToList();

                foreach (var wixSymbol in wixSymbols)
                {
                    this.VariableCache[wixSymbol.Id.Id] = wixSymbol.Value;
                }
            }
        }
    }
}
