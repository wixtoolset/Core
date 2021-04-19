// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.Bind
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using WixToolset.Data;
    using WixToolset.Extensibility.Data;
    using WixToolset.Extensibility.Services;

    /// <summary>
    /// Resolves the fields which had variables that needed to be resolved after the file information
    /// was loaded.
    /// </summary>
    internal class ResolveDelayedFieldsCommand
    {
        /// <summary>
        /// Resolve delayed fields.
        /// </summary>
        /// <param name="messaging"></param>
        /// <param name="delayedFields">The fields which had resolution delayed.</param>
        /// <param name="variableCache">The cached variable values used when resolving delayed fields.</param>
        public ResolveDelayedFieldsCommand(IMessaging messaging, IEnumerable<IDelayedField> delayedFields, Dictionary<string, string> variableCache)
        {
            this.Messaging = messaging;
            this.DelayedFields = delayedFields;
            this.VariableCache = variableCache;
        }

        private IMessaging Messaging { get; }

        private IEnumerable<IDelayedField> DelayedFields { get;}

        private IDictionary<string, string> VariableCache { get; }

        public void Execute()
        {
            var deferredFields = new List<IDelayedField>();

            foreach (var delayedField in this.DelayedFields)
            {
                try
                {
                    var propertySymbol = delayedField.Symbol;

                    // process properties first in case they refer to other binder variables
                    if (delayedField.Symbol.Definition.Type == SymbolDefinitionType.Property)
                    {
                        var value = this.ResolveDelayedVariables(propertySymbol.SourceLineNumbers, delayedField.Field.AsString());

                        // update the variable cache with the new value
                        var key = String.Concat("property.", propertySymbol.Id.Id);
                        this.VariableCache[key] = value;

                        // update the field data
                        delayedField.Field.Set(value);
                    }
                    else
                    {
                        deferredFields.Add(delayedField);
                    }
                }
                catch (WixException we)
                {
                    this.Messaging.Write(we.Error);
                    continue;
                }
            }

            // add specialization for ProductVersion fields
            var keyProductVersion = "property.ProductVersion";
            if (this.VariableCache.TryGetValue(keyProductVersion, out var versionValue) && Version.TryParse(versionValue, out var productVersion))
            {
                // Don't add the variable if it already exists (developer defined a property with the same name).
                var fieldKey = String.Concat(keyProductVersion, ".Major");
                if (!this.VariableCache.ContainsKey(fieldKey))
                {
                    this.VariableCache[fieldKey] = productVersion.Major.ToString(CultureInfo.InvariantCulture);
                }

                fieldKey = String.Concat(keyProductVersion, ".Minor");
                if (!this.VariableCache.ContainsKey(fieldKey))
                {
                    this.VariableCache[fieldKey] = productVersion.Minor.ToString(CultureInfo.InvariantCulture);
                }

                fieldKey = String.Concat(keyProductVersion, ".Build");
                if (!this.VariableCache.ContainsKey(fieldKey))
                {
                    this.VariableCache[fieldKey] = productVersion.Build.ToString(CultureInfo.InvariantCulture);
                }

                fieldKey = String.Concat(keyProductVersion, ".Revision");
                if (!this.VariableCache.ContainsKey(fieldKey))
                {
                    this.VariableCache[fieldKey] = productVersion.Revision.ToString(CultureInfo.InvariantCulture);
                }
            }

            // process the remaining fields in case they refer to property binder variables
            foreach (var delayedField in deferredFields)
            {
                try
                {
                    var value = this.ResolveDelayedVariables(delayedField.Symbol.SourceLineNumbers, delayedField.Field.AsString());
                    delayedField.Field.Set(value);
                }
                catch (WixException we)
                {
                    this.Messaging.Write(we.Error);
                }
            }
        }

        private string ResolveDelayedVariables(SourceLineNumber sourceLineNumbers, string value)
        {
            var start = 0;

            while (Common.TryParseWixVariable(value, start, out var parsed))
            {
                string key;

                if (parsed.Namespace == "bind")
                {
                    key = null == parsed.Scope ? parsed.Name : $"{parsed.Name}.{parsed.Scope}";

                    if (!this.VariableCache.TryGetValue(key, out var resolvedValue))
                    {
                        resolvedValue = parsed.DefaultValue;
                    }

                    // insert the resolved value if it was found or display an error
                    if (null != resolvedValue)
                    {
                        if (parsed.Index == 0 && parsed.Length == value.Length)
                        {
                            value = resolvedValue;
                        }
                        else
                        {
                            var sb = new StringBuilder(value);
                            sb.Remove(parsed.Index, parsed.Length);
                            sb.Insert(parsed.Index, resolvedValue);
                            value = sb.ToString();
                        }

                        start = parsed.Index;
                    }
                    else
                    {
                        this.Messaging.Write(ErrorMessages.UnresolvedBindReference(sourceLineNumbers, value));
                        start = parsed.Index + parsed.Length;
                    }
                }
                else
                {
                    start = parsed.Index + parsed.Length;
                }
            }

            return value;
        }
    }
}
