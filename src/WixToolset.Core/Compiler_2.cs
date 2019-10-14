// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Xml.Linq;
    using WixToolset.Data;
    using WixToolset.Data.Tuples;
    using WixToolset.Data.WindowsInstaller;
    using WixToolset.Extensibility;

    /// <summary>
    /// Compiler of the WiX toolset.
    /// </summary>
    internal partial class Compiler : ICompiler
    {
        /// <summary>
        /// Parses a product element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        private void ParseProductElement(XElement node)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            var codepage = 65001;
            string productCode = "*";
            string upgradeCode = null;
            string manufacturer = null;
            string version = null;
            string symbols = null;

            this.activeName = null;
            this.activeLanguage = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        productCode = this.Core.GetAttributeGuidValue(sourceLineNumbers, attrib, true);
                        break;
                    case "Codepage":
                        codepage = this.Core.GetAttributeCodePageValue(sourceLineNumbers, attrib);
                        break;
                    case "Language":
                        this.activeLanguage = this.Core.GetAttributeLocalizableIntegerValue(sourceLineNumbers, attrib, 0, Int16.MaxValue);
                        break;
                    case "Manufacturer":
                        manufacturer = this.Core.GetAttributeValue(sourceLineNumbers, attrib, EmptyRule.MustHaveNonWhitespaceCharacters);
                        if ("PUT-COMPANY-NAME-HERE" == manufacturer)
                        {
                            this.Core.Write(WarningMessages.PlaceholderValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, manufacturer));
                        }
                        break;
                    case "Name":
                        this.activeName = this.Core.GetAttributeValue(sourceLineNumbers, attrib, EmptyRule.MustHaveNonWhitespaceCharacters);
                        if ("PUT-PRODUCT-NAME-HERE" == this.activeName)
                        {
                            this.Core.Write(WarningMessages.PlaceholderValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, this.activeName));
                        }
                        break;
                    case "UpgradeCode":
                        upgradeCode = this.Core.GetAttributeGuidValue(sourceLineNumbers, attrib, false);
                        break;
                    case "Version": // if the attribute is valid version, use the attribute value as is (so "1.0000.01.01" would *not* get translated to "1.0.1.1").
                        var verifiedVersion = this.Core.GetAttributeVersionValue(sourceLineNumbers, attrib);
                        if (!String.IsNullOrEmpty(verifiedVersion))
                        {
                            version = attrib.Value;
                        }
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == productCode)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Id"));
            }

            if (null == this.activeLanguage)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Language"));
            }

            if (null == manufacturer)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Manufacturer"));
            }

            if (null == this.activeName)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Name"));
            }

            if (null == upgradeCode)
            {
                this.Core.Write(WarningMessages.MissingUpgradeCode(sourceLineNumbers));
            }

            if (null == version)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Version"));
            }
            else if (!CompilerCore.IsValidProductVersion(version))
            {
                this.Core.Write(ErrorMessages.InvalidProductVersion(sourceLineNumbers, version));
            }

            if (this.Core.EncounteredError)
            {
                return;
            }

            try
            {
                this.compilingProduct = true;
                this.Core.CreateActiveSection(productCode, SectionType.Product, codepage, this.Context.CompilationId);

                this.AddProperty(sourceLineNumbers, new Identifier(AccessModifier.Public, "Manufacturer"), manufacturer, false, false, false, true);
                this.AddProperty(sourceLineNumbers, new Identifier(AccessModifier.Public, "ProductCode"), productCode, false, false, false, true);
                this.AddProperty(sourceLineNumbers, new Identifier(AccessModifier.Public, "ProductLanguage"), this.activeLanguage, false, false, false, true);
                this.AddProperty(sourceLineNumbers, new Identifier(AccessModifier.Public, "ProductName"), this.activeName, false, false, false, true);
                this.AddProperty(sourceLineNumbers, new Identifier(AccessModifier.Public, "ProductVersion"), version, false, false, false, true);
                if (null != upgradeCode)
                {
                    this.AddProperty(sourceLineNumbers, new Identifier(AccessModifier.Public, "UpgradeCode"), upgradeCode, false, false, false, true);
                }

                var contextValues = new Dictionary<string, string>
                {
                    ["ProductLanguage"] = this.activeLanguage,
                    ["ProductVersion"] = version,
                    ["UpgradeCode"] = upgradeCode
                };

                var featureDisplay = 0;
                foreach (var child in node.Elements())
                {
                    if (CompilerCore.WixNamespace == child.Name.Namespace)
                    {
                        switch (child.Name.LocalName)
                        {
                        case "_locDefinition":
                            break;
                        case "AdminExecuteSequence":
                            this.ParseSequenceElement(child, SequenceTable.AdminExecuteSequence);
                            break;
                        case "AdminUISequence":
                            this.ParseSequenceElement(child, SequenceTable.AdminUISequence);
                            break;
                        case "AdvertiseExecuteSequence":
                            this.ParseSequenceElement(child, SequenceTable.AdvertiseExecuteSequence);
                            break;
                        case "InstallExecuteSequence":
                            this.ParseSequenceElement(child, SequenceTable.InstallExecuteSequence);
                            break;
                        case "InstallUISequence":
                            this.ParseSequenceElement(child, SequenceTable.InstallUISequence);
                            break;
                        case "AppId":
                            this.ParseAppIdElement(child, null, YesNoType.Yes, null, null, null);
                            break;
                        case "Binary":
                            this.ParseBinaryElement(child);
                            break;
                        case "ComplianceCheck":
                            this.ParseComplianceCheckElement(child);
                            break;
                        case "Component":
                            this.ParseComponentElement(child, ComplexReferenceParentType.Unknown, null, null, CompilerConstants.IntegerNotSet, null, null);
                            break;
                        case "ComponentGroup":
                            this.ParseComponentGroupElement(child, ComplexReferenceParentType.Unknown, null);
                            break;
                        case "Condition":
                            this.ParseConditionElement(child, node.Name.LocalName, null, null);
                            break;
                        case "CustomAction":
                            this.ParseCustomActionElement(child);
                            break;
                        case "CustomActionRef":
                            this.ParseSimpleRefElement(child, "CustomAction");
                            break;
                        case "CustomTable":
                            this.ParseCustomTableElement(child);
                            break;
                        case "Directory":
                            this.ParseDirectoryElement(child, null, CompilerConstants.IntegerNotSet, String.Empty);
                            break;
                        case "DirectoryRef":
                            this.ParseDirectoryRefElement(child);
                            break;
                        case "EmbeddedChainer":
                            this.ParseEmbeddedChainerElement(child);
                            break;
                        case "EmbeddedChainerRef":
                            this.ParseSimpleRefElement(child, "MsiEmbeddedChainer");
                            break;
                        case "EnsureTable":
                            this.ParseEnsureTableElement(child);
                            break;
                        case "Feature":
                            this.ParseFeatureElement(child, ComplexReferenceParentType.Product, productCode, ref featureDisplay);
                            break;
                        case "FeatureRef":
                            this.ParseFeatureRefElement(child, ComplexReferenceParentType.Product, productCode);
                            break;
                        case "FeatureGroupRef":
                            this.ParseFeatureGroupRefElement(child, ComplexReferenceParentType.Product, productCode);
                            break;
                        case "Icon":
                            this.ParseIconElement(child);
                            break;
                        case "InstanceTransforms":
                            this.ParseInstanceTransformsElement(child);
                            break;
                        case "MajorUpgrade":
                            this.ParseMajorUpgradeElement(child, contextValues);
                            break;
                        case "Media":
                            this.ParseMediaElement(child, null);
                            break;
                        case "MediaTemplate":
                            this.ParseMediaTemplateElement(child, null);
                            break;
                        case "Package":
                            this.ParsePackageElement(child, manufacturer, null);
                            break;
                        case "PackageCertificates":
                        case "PatchCertificates":
                            this.ParseCertificatesElement(child);
                            break;
                        case "Property":
                            this.ParsePropertyElement(child);
                            break;
                        case "PropertyRef":
                            this.ParseSimpleRefElement(child, "Property");
                            break;
                        case "SetDirectory":
                            this.ParseSetDirectoryElement(child);
                            break;
                        case "SetProperty":
                            this.ParseSetPropertyElement(child);
                            break;
                        case "SFPCatalog":
                            string parentName = null;
                            this.ParseSFPCatalogElement(child, ref parentName);
                            break;
                        case "SymbolPath":
                            if (null != symbols)
                            {
                                symbols += ";" + this.ParseSymbolPathElement(child);
                            }
                            else
                            {
                                symbols = this.ParseSymbolPathElement(child);
                            }
                            break;
                        case "UI":
                            this.ParseUIElement(child);
                            break;
                        case "UIRef":
                            this.ParseSimpleRefElement(child, "WixUI");
                            break;
                        case "Upgrade":
                            this.ParseUpgradeElement(child);
                            break;
                        case "WixVariable":
                            this.ParseWixVariableElement(child);
                            break;
                        default:
                            this.Core.UnexpectedElement(node, child);
                            break;
                        }
                    }
                    else
                    {
                        this.Core.ParseExtensionElement(node, child);
                    }
                }

                if (!this.Core.EncounteredError)
                {
                    if (null != symbols)
                    {
                        var tuple = new WixDeltaPatchSymbolPathsTuple(sourceLineNumbers)
                        {
                            SymbolId = productCode,
                            SymbolType = SymbolPathType.Product,
                            SymbolPaths = symbols
                        };

                        this.Core.AddTuple(tuple);
                    }
                }
            }
            finally
            {
                this.compilingProduct = false;
            }
        }

        /// <summary>
        /// Parses an odbc driver or translator element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="componentId">Identifier of parent component.</param>
        /// <param name="fileId">Default identifer for driver/translator file.</param>
        /// <param name="tupleDefinitionType">Tuple type we're processing for.</param>
        private void ParseODBCDriverOrTranslator(XElement node, string componentId, string fileId, TupleDefinitionType tupleDefinitionType)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            var driver = fileId;
            string name = null;
            var setup = fileId;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "File":
                        driver = this.Core.GetAttributeIdentifierValue(sourceLineNumbers, attrib);
                        this.Core.CreateSimpleReference(sourceLineNumbers, "File", driver);
                        break;
                    case "Name":
                        name = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "SetupFile":
                        setup = this.Core.GetAttributeIdentifierValue(sourceLineNumbers, attrib);
                        this.Core.CreateSimpleReference(sourceLineNumbers, "File", setup);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == name)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Name"));
            }

            if (null == id)
            {
                id = this.Core.CreateIdentifier("odb", name, fileId, setup);
            }

            // drivers have a few possible children
            if (TupleDefinitionType.ODBCDriver == tupleDefinitionType)
            {
                // process any data sources for the driver
                foreach (var child in node.Elements())
                {
                    if (CompilerCore.WixNamespace == child.Name.Namespace)
                    {
                        switch (child.Name.LocalName)
                        {
                        case "ODBCDataSource":
                            string ignoredKeyPath = null;
                            this.ParseODBCDataSource(child, componentId, name, out ignoredKeyPath);
                            break;
                        case "Property":
                            this.ParseODBCProperty(child, id.Id, TupleDefinitionType.ODBCAttribute);
                            break;
                        default:
                            this.Core.UnexpectedElement(node, child);
                            break;
                        }
                    }
                    else
                    {
                        this.Core.ParseExtensionElement(node, child);
                    }
                }
            }
            else
            {
                this.Core.ParseForExtensionElements(node);
            }

            if (!this.Core.EncounteredError)
            {
                var tuple = this.Core.CreateTuple(sourceLineNumbers, tupleDefinitionType, id);
                tuple.Set(1, componentId);
                tuple.Set(2, name);
                tuple.Set(3, driver);
                tuple.Set(4, setup);
            }
        }

        /// <summary>
        /// Parses a Property element underneath an ODBC driver or translator.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="parentId">Identifier of parent driver or translator.</param>
        /// <param name="tupleDefinitionType">Name of the table to create property in.</param>
        private void ParseODBCProperty(XElement node, string parentId, TupleDefinitionType tupleDefinitionType)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string id = null;
            string propertyValue = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Value":
                        propertyValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == id)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Id"));
            }

            this.Core.ParseForExtensionElements(node);

            if (!this.Core.EncounteredError)
            {
                var tuple = this.Core.CreateTuple(sourceLineNumbers, tupleDefinitionType, new Identifier(AccessModifier.Private, parentId, id));
                tuple.Set(0, parentId);
                tuple.Set(1, id);
                tuple.Set(2, propertyValue);
            }
        }

        /// <summary>
        /// Parse an odbc data source element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="componentId">Identifier of parent component.</param>
        /// <param name="driverName">Default name of driver.</param>
        /// <param name="possibleKeyPath">Identifier of this element in case it is a keypath.</param>
        /// <returns>Yes if this element was marked as the parent component's key path, No if explicitly marked as not being a key path, or NotSet otherwise.</returns>
        private YesNoType ParseODBCDataSource(XElement node, string componentId, string driverName, out string possibleKeyPath)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            var keyPath = YesNoType.NotSet;
            string name = null;
            var registration = CompilerConstants.IntegerNotSet;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "DriverName":
                        driverName = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "KeyPath":
                        keyPath = this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "Name":
                        name = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Registration":
                        var registrationValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (registrationValue)
                        {
                        case "machine":
                            registration = 0;
                            break;
                        case "user":
                            registration = 1;
                            break;
                        case "":
                            break;
                        default:
                            this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, "Registration", registrationValue, "machine", "user"));
                            break;
                        }
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (CompilerConstants.IntegerNotSet == registration)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Registration"));
                registration = CompilerConstants.IllegalInteger;
            }

            if (null == id)
            {
                id = this.Core.CreateIdentifier("odc", name, driverName, registration.ToString());
            }

            foreach (var child in node.Elements())
            {
                if (CompilerCore.WixNamespace == child.Name.Namespace)
                {
                    switch (child.Name.LocalName)
                    {
                    case "Property":
                        this.ParseODBCProperty(child, id.Id, TupleDefinitionType.ODBCSourceAttribute);
                        break;
                    default:
                        this.Core.UnexpectedElement(node, child);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionElement(node, child);
                }
            }

            if (!this.Core.EncounteredError)
            {
                this.Core.AddTuple(new ODBCDataSourceTuple(sourceLineNumbers, id)
                {
                    ComponentRef = componentId,
                    Description = name,
                    DriverDescription = driverName,
                    Registration = registration
                });
            }

            possibleKeyPath = id.Id;
            return keyPath;
        }

        /// <summary>
        /// Parses a package element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="productAuthor">Default package author.</param>
        /// <param name="moduleId">The module guid - this is necessary until Module/@Guid is removed.</param>
        private void ParsePackageElement(XElement node, string productAuthor, string moduleId)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            var codepage = "1252";
            var comments = String.Format(CultureInfo.InvariantCulture, "This installer database contains the logic and data required to install {0}.", this.activeName);
            var keywords = "Installer";
            var msiVersion = 100; // lowest released version, really should be specified
            var packageAuthor = productAuthor;
            string packageCode = null;
            var packageLanguages = this.activeLanguage;
            var packageName = this.activeName;
            string platform = null;
            string platformValue = null;
            var security = YesNoDefaultType.Default;
            var sourceBits = (this.compilingModule ? 2 : 0);
            var installPrivilegeSeen = false;
            var installScopeSeen = false;

            switch (this.CurrentPlatform)
            {
            case Platform.X86:
                platform = "Intel";
                break;
            case Platform.X64:
                platform = "x64";
                msiVersion = 200;
                break;
            case Platform.IA64:
                platform = "Intel64";
                msiVersion = 200;
                break;
            case Platform.ARM:
                platform = "Arm";
                msiVersion = 500;
                break;
            default:
                throw new ArgumentException("Unknown platform enumeration '{0}' encountered.", this.CurrentPlatform.ToString());
            }

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        packageCode = this.Core.GetAttributeGuidValue(sourceLineNumbers, attrib, this.compilingProduct);
                        break;
                    case "AdminImage":
                        if (YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib))
                        {
                            sourceBits |= 4;
                        }
                        break;
                    case "Comments":
                        comments = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Compressed":
                        // merge modules must always be compressed, so this attribute is invalid
                        if (this.compilingModule)
                        {
                            this.Core.Write(WarningMessages.DeprecatedPackageCompressedAttribute(sourceLineNumbers));
                            // this.core.OnMessage(WixErrors.IllegalAttributeWhenNested(sourceLineNumbers, node.Name.LocalName, "Compressed", "Module"));
                        }
                        else if (YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib))
                        {
                            sourceBits |= 2;
                        }
                        break;
                    case "Description":
                        packageName = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "InstallPrivileges":
                        var installPrivileges = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (installPrivileges)
                        {
                        case "elevated":
                            // this is the default setting
                            installPrivilegeSeen = true;
                            break;
                        case "limited":
                            sourceBits |= 8;
                            installPrivilegeSeen = true;
                            break;
                        case "":
                            break;
                        default:
                            this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, installPrivileges, "elevated", "limited"));
                            break;
                        }
                        break;
                    case "InstallScope":
                        var installScope = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (installScope)
                        {
                        case "perMachine":
                            {
                            this.Core.AddTuple(new PropertyTuple(sourceLineNumbers, new Identifier(AccessModifier.Public, "ALLUSERS"))
                            {
                                Value = "1"
                            });
                            installScopeSeen = true;
                            }
                            break;
                        case "perUser":
                            sourceBits |= 8;
                            installScopeSeen = true;
                            break;
                        case "":
                            break;
                        default:
                            this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, installScope, "perMachine", "perUser"));
                            break;
                        }
                        break;
                    case "InstallerVersion":
                        msiVersion = this.Core.GetAttributeIntegerValue(sourceLineNumbers, attrib, 0, Int32.MaxValue);
                        break;
                    case "Keywords":
                        keywords = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Languages":
                        packageLanguages = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Manufacturer":
                        packageAuthor = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        if ("PUT-COMPANY-NAME-HERE" == packageAuthor)
                        {
                            this.Core.Write(WarningMessages.PlaceholderValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, packageAuthor));
                        }
                        break;
                    case "Platform":
                        if (null != platformValue)
                        {
                            this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, "Platforms"));
                        }

                        platformValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (platformValue)
                        {
                        case "intel":
                            this.Core.Write(WarningMessages.DeprecatedAttributeValue(sourceLineNumbers, platformValue, node.Name.LocalName, attrib.Name.LocalName, "x86"));
                            goto case "x86";
                        case "x86":
                            platform = "Intel";
                            break;
                        case "x64":
                            platform = "x64";
                            break;
                        case "intel64":
                            this.Core.Write(WarningMessages.DeprecatedAttributeValue(sourceLineNumbers, platformValue, node.Name.LocalName, attrib.Name.LocalName, "ia64"));
                            goto case "ia64";
                        case "ia64":
                            platform = "Intel64";
                            break;
                        case "arm":
                            platform = "Arm";
                            break;
                        case "":
                            break;
                        default:
                            this.Core.Write(ErrorMessages.InvalidPlatformValue(sourceLineNumbers, platformValue));
                            break;
                        }
                        break;
                    case "Platforms":
                        if (null != platformValue)
                        {
                            this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, "Platform"));
                        }

                        this.Core.Write(WarningMessages.DeprecatedAttribute(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, "Platform"));
                        platformValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        platform = platformValue;
                        break;
                    case "ReadOnly":
                        security = this.Core.GetAttributeYesNoDefaultValue(sourceLineNumbers, attrib);
                        break;
                    case "ShortNames":
                        if (YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib))
                        {
                            sourceBits |= 1;
                        }
                        break;
                    case "SummaryCodepage":
                        codepage = this.Core.GetAttributeLocalizableCodePageValue(sourceLineNumbers, attrib, true);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (installPrivilegeSeen && installScopeSeen)
            {
                this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "InstallPrivileges", "InstallScope"));
            }

            if ((0 != String.Compare(platform, "Intel", StringComparison.OrdinalIgnoreCase)) && 200 > msiVersion)
            {
                msiVersion = 200;
                this.Core.Write(WarningMessages.RequiresMsi200for64bitPackage(sourceLineNumbers));
            }

            if ((0 == String.Compare(platform, "Arm", StringComparison.OrdinalIgnoreCase)) && 500 > msiVersion)
            {
                msiVersion = 500;
                this.Core.Write(WarningMessages.RequiresMsi500forArmPackage(sourceLineNumbers));
            }

            if (null == packageAuthor)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Manufacturer"));
            }

            if (this.compilingModule)
            {
                if (null == packageCode)
                {
                    this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Id"));
                }

                // merge modules use the modularization guid as the package code
                if (null != moduleId)
                {
                    packageCode = moduleId;
                }

                // merge modules are always compressed
                sourceBits = 2;
            }
            else // product
            {
                if (null == packageCode)
                {
                    packageCode = "*";
                }

                if ("*" != packageCode)
                {
                    this.Core.Write(WarningMessages.PackageCodeSet(sourceLineNumbers));
                }
            }

            this.Core.ParseForExtensionElements(node);

            if (!this.Core.EncounteredError)
            {
                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.Codepage,
                    Value = codepage
                });

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.Title,
                    Value = "Installation Database"
                });

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.Subject,
                    Value = packageName
                });

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.Author,
                    Value = packageAuthor
                });

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.Keywords,
                    Value = keywords
                });

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.Comments,
                    Value = comments
                });

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.PlatformAndLanguage,
                    Value = String.Format(CultureInfo.InvariantCulture, "{0};{1}", platform, packageLanguages)
                });

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.PackageCode,
                    Value = packageCode
                });

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.WindowsInstallerVersion,
                    Value = msiVersion.ToString(CultureInfo.InvariantCulture)
                });

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.WordCount,
                    Value = sourceBits.ToString(CultureInfo.InvariantCulture)
                });

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.Security,
                    Value = YesNoDefaultType.No == security ? "0" : YesNoDefaultType.Yes == security ? "4" : "2"
                });
            }
        }

        /// <summary>
        /// Parses a patch information element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        private void ParsePatchInformationElement(XElement node)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            var codepage = "1252";
            string comments = null;
            var keywords = "Installer,Patching,PCP,Database";
            var msiVersion = 1; // Should always be 1 for patches
            string packageAuthor = null;
            var packageName = this.activeName;
            var security = YesNoDefaultType.Default;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "AdminImage":
                        this.Core.Write(WarningMessages.DeprecatedAttribute(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName));
                        break;
                    case "Comments":
                        comments = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Compressed":
                        this.Core.Write(WarningMessages.DeprecatedAttribute(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName));
                        break;
                    case "Description":
                        packageName = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Keywords":
                        keywords = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Languages":
                        this.Core.Write(WarningMessages.DeprecatedAttribute(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName));
                        break;
                    case "Manufacturer":
                        packageAuthor = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Platforms":
                        this.Core.Write(WarningMessages.DeprecatedAttribute(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName));
                        break;
                    case "ReadOnly":
                        security = this.Core.GetAttributeYesNoDefaultValue(sourceLineNumbers, attrib);
                        break;
                    case "ShortNames":
                        this.Core.Write(WarningMessages.DeprecatedAttribute(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName));
                        break;
                    case "SummaryCodepage":
                        codepage = this.Core.GetAttributeLocalizableCodePageValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            this.Core.ParseForExtensionElements(node);

            if (!this.Core.EncounteredError)
            {
                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.Codepage,
                    Value = codepage
                });

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.Title,
                    Value = "Patch"
                });

                if (null != packageName)
                {
                    this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                    {
                        PropertyId = SumaryInformationType.Subject,
                        Value = packageName
                    });
                }

                if (null != packageAuthor)
                {
                    this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                    {
                        PropertyId = SumaryInformationType.Author,
                        Value = packageAuthor
                    });
                }

                if (null != keywords)
                {
                    this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                    {
                        PropertyId = SumaryInformationType.Keywords,
                        Value = keywords
                    });
                }

                if (null != comments)
                {
                    this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                    {
                        PropertyId = SumaryInformationType.Comments,
                        Value = comments
                    });
                }

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.WindowsInstallerVersion,
                    Value = msiVersion.ToString(CultureInfo.InvariantCulture)
                });

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.WordCount,
                    Value = "0"
                });

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.WindowsInstallerVersion,
                    Value = msiVersion.ToString(CultureInfo.InvariantCulture)
                });

                this.Core.AddTuple(new SummaryInformationTuple(sourceLineNumbers)
                {
                    PropertyId = SumaryInformationType.Security,
                    Value = YesNoDefaultType.No == security ? "0" : YesNoDefaultType.Yes == security ? "4" : "2"
                });
            }
        }

        /// <summary>
        /// Parses a permission element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="objectId">Identifier of object to be secured.</param>
        /// <param name="tableName">Name of table that contains objectId.</param>
        private void ParsePermissionElement(XElement node, string objectId, string tableName)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            var bits = new BitArray(32);
            string domain = null;
            string[] specialPermissions = null;
            string user = null;

            switch (tableName)
            {
            case "CreateFolder":
                specialPermissions = Common.FolderPermissions;
                break;
            case "File":
                specialPermissions = Common.FilePermissions;
                break;
            case "Registry":
                specialPermissions = Common.RegistryPermissions;
                break;
            default:
                this.Core.UnexpectedElement(node.Parent, node);
                return; // stop processing this element since no valid permissions are available
            }

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Domain":
                        domain = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "User":
                        user = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "FileAllRights":
                        // match the WinNT.h mask FILE_ALL_ACCESS for value 0x001F01FF (aka 1 1111 0000 0001 1111 1111 or 2032127)
                        bits[0] = bits[1] = bits[2] = bits[3] = bits[4] = bits[5] = bits[6] = bits[7] = bits[8] = bits[16] = bits[17] = bits[18] = bits[19] = bits[20] = true;
                        break;
                    case "SpecificRightsAll":
                        // match the WinNT.h mask SPECIFIC_RIGHTS_ALL for value 0x0000FFFF (aka 1111 1111 1111 1111)
                        bits[0] = bits[1] = bits[2] = bits[3] = bits[4] = bits[5] = bits[6] = bits[7] = bits[8] = bits[9] = bits[10] = bits[11] = bits[12] = bits[13] = bits[14] = bits[15] = true;
                        break;
                    default:
                        var attribValue = this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        if (!this.Core.TrySetBitFromName(Common.StandardPermissions, attrib.Name.LocalName, attribValue, bits, 16))
                        {
                            if (!this.Core.TrySetBitFromName(Common.GenericPermissions, attrib.Name.LocalName, attribValue, bits, 28))
                            {
                                if (!this.Core.TrySetBitFromName(specialPermissions, attrib.Name.LocalName, attribValue, bits, 0))
                                {
                                    this.Core.UnexpectedAttribute(node, attrib);
                                    break;
                                }
                            }
                        }
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == user)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "User"));
            }

            var permission = this.Core.CreateIntegerFromBitArray(bits);

            if (Int32.MinValue == permission) // just GENERIC_READ, which is MSI_NULL
            {
                this.Core.Write(ErrorMessages.GenericReadNotAllowed(sourceLineNumbers));
            }

            this.Core.ParseForExtensionElements(node);

            if (!this.Core.EncounteredError)
            {
                this.Core.AddTuple(new LockPermissionsTuple(sourceLineNumbers)
                {
                    LockObject = objectId,
                    Table = tableName,
                    Domain = domain,
                    User = user,
                    Permission = permission
                });
            }
        }

        /// <summary>
        /// Parses an extended permission element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="objectId">Identifier of object to be secured.</param>
        /// <param name="tableName">Name of table that contains objectId.</param>
        private void ParsePermissionExElement(XElement node, string objectId, string tableName)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string condition = null;
            Identifier id = null;
            string sddl = null;

            switch (tableName)
            {
            case "CreateFolder":
            case "File":
            case "Registry":
            case "ServiceInstall":
                break;
            default:
                this.Core.UnexpectedElement(node.Parent, node);
                return; // stop processing this element since nothing will be valid.
            }

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Sddl":
                        sddl = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == sddl)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Sddl"));
            }

            if (null == id)
            {
                id = this.Core.CreateIdentifier("pme", objectId, tableName, sddl);
            }

            foreach (var child in node.Elements())
            {
                if (CompilerCore.WixNamespace == child.Name.Namespace)
                {
                    switch (child.Name.LocalName)
                    {
                    case "Condition":
                        if (null != condition)
                        {
                            var childSourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
                            this.Core.Write(ErrorMessages.TooManyChildren(childSourceLineNumbers, node.Name.LocalName, child.Name.LocalName));
                        }

                        condition = this.ParseConditionElement(child, node.Name.LocalName, null, null);
                        break;
                    default:
                        this.Core.UnexpectedElement(node, child);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionElement(node, child);
                }
            }

            if (!this.Core.EncounteredError)
            {
                this.Core.AddTuple(new MsiLockPermissionsExTuple(sourceLineNumbers, id)
                {
                    LockObject = objectId,
                    Table = tableName,
                    SDDLText =sddl,
                    Condition = condition
                });
            }
        }

        /// <summary>
        /// Parses a progid element
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="componentId">Identifier of parent component.</param>
        /// <param name="advertise">Flag if progid is advertised.</param>
        /// <param name="classId">CLSID related to ProgId.</param>
        /// <param name="description">Default description of ProgId</param>
        /// <param name="parent">Optional parent ProgId</param>
        /// <param name="foundExtension">Set to true if an extension is found; used for error-checking.</param>
        /// <param name="firstProgIdForClass">Whether or not this ProgId is the first one found in the parent class.</param>
        /// <returns>This element's Id.</returns>
        private string ParseProgIdElement(XElement node, string componentId, YesNoType advertise, string classId, string description, string parent, ref bool foundExtension, YesNoType firstProgIdForClass)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string icon = null;
            var iconIndex = CompilerConstants.IntegerNotSet;
            string noOpen = null;
            string progId = null;
            var progIdAdvertise = YesNoType.NotSet;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        progId = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Advertise":
                        progIdAdvertise = this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "Description":
                        description = this.Core.GetAttributeValue(sourceLineNumbers, attrib, EmptyRule.CanBeEmpty);
                        break;
                    case "Icon":
                        icon = this.Core.GetAttributeIdentifierValue(sourceLineNumbers, attrib);
                        break;
                    case "IconIndex":
                        iconIndex = this.Core.GetAttributeIntegerValue(sourceLineNumbers, attrib, Int16.MinValue + 1, Int16.MaxValue);
                        break;
                    case "NoOpen":
                        noOpen = this.Core.GetAttributeValue(sourceLineNumbers, attrib, EmptyRule.CanBeEmpty);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if ((YesNoType.No == advertise && YesNoType.Yes == progIdAdvertise) || (YesNoType.Yes == advertise && YesNoType.No == progIdAdvertise))
            {
                this.Core.Write(ErrorMessages.AdvertiseStateMustMatch(sourceLineNumbers, advertise.ToString(), progIdAdvertise.ToString()));
            }
            else
            {
                advertise = progIdAdvertise;
            }

            if (YesNoType.NotSet == advertise)
            {
                advertise = YesNoType.No;
            }

            if (null != parent && (null != icon || CompilerConstants.IntegerNotSet != iconIndex))
            {
                this.Core.Write(ErrorMessages.VersionIndependentProgIdsCannotHaveIcons(sourceLineNumbers));
            }

            var firstProgIdForNestedClass = YesNoType.Yes;
            foreach (var child in node.Elements())
            {
                if (CompilerCore.WixNamespace == child.Name.Namespace)
                {
                    switch (child.Name.LocalName)
                    {
                    case "Extension":
                        this.ParseExtensionElement(child, componentId, advertise, progId);
                        foundExtension = true;
                        break;
                    case "ProgId":
                        // Only allow one nested ProgId.  If we have a child, we should not have a parent.
                        if (null == parent)
                        {
                            if (YesNoType.Yes == advertise)
                            {
                                this.ParseProgIdElement(child, componentId, advertise, null, description, progId, ref foundExtension, firstProgIdForNestedClass);
                            }
                            else if (YesNoType.No == advertise)
                            {
                                this.ParseProgIdElement(child, componentId, advertise, classId, description, progId, ref foundExtension, firstProgIdForNestedClass);
                            }

                            firstProgIdForNestedClass = YesNoType.No; // any ProgId after this one is definitely not the first.
                        }
                        else
                        {
                            var childSourceLineNumbers = Preprocessor.GetSourceLineNumbers(child);
                            this.Core.Write(ErrorMessages.ProgIdNestedTooDeep(childSourceLineNumbers));
                        }
                        break;
                    default:
                        this.Core.UnexpectedElement(node, child);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionElement(node, child);
                }
            }

            if (YesNoType.Yes == advertise)
            {
                if (!this.Core.EncounteredError)
                {
                    var tuple = new ProgIdTuple(sourceLineNumbers, new Identifier(AccessModifier.Public, progId))
                    {
                        ProgId = progId,
                        ParentProgIdRef = parent,
                        ClassRef = classId,
                        Description = description,
                    };

                    if (null != icon)
                    {
                        tuple.IconRef = icon;
                        this.Core.CreateSimpleReference(sourceLineNumbers, "Icon", icon);
                    }

                    if (CompilerConstants.IntegerNotSet != iconIndex)
                    {
                        tuple.IconIndex = iconIndex;
                    }

                    this.Core.AddTuple(tuple);

                    this.Core.EnsureTable(sourceLineNumbers, "Class");
                }
            }
            else if (YesNoType.No == advertise)
            {
                this.Core.CreateRegistryRow(sourceLineNumbers, RegistryRootType.ClassesRoot, progId, String.Empty, description, componentId);
                if (null != classId)
                {
                    this.Core.CreateRegistryRow(sourceLineNumbers, RegistryRootType.ClassesRoot, String.Concat(progId, "\\CLSID"), String.Empty, classId, componentId);
                    if (null != parent)   // if this is a version independent ProgId
                    {
                        if (YesNoType.Yes == firstProgIdForClass)
                        {
                            this.Core.CreateRegistryRow(sourceLineNumbers, RegistryRootType.ClassesRoot, String.Concat("CLSID\\", classId, "\\VersionIndependentProgID"), String.Empty, progId, componentId);
                        }

                        this.Core.CreateRegistryRow(sourceLineNumbers, RegistryRootType.ClassesRoot, String.Concat(progId, "\\CurVer"), String.Empty, parent, componentId);
                    }
                    else
                    {
                        if (YesNoType.Yes == firstProgIdForClass)
                        {
                            this.Core.CreateRegistryRow(sourceLineNumbers, RegistryRootType.ClassesRoot, String.Concat("CLSID\\", classId, "\\ProgID"), String.Empty, progId, componentId);
                        }
                    }
                }

                if (null != icon)   // ProgId's Default Icon
                {
                    this.Core.CreateSimpleReference(sourceLineNumbers, "File", icon);

                    icon = String.Format(CultureInfo.InvariantCulture, "\"[#{0}]\"", icon);

                    if (CompilerConstants.IntegerNotSet != iconIndex)
                    {
                        icon = String.Concat(icon, ",", iconIndex);
                    }

                    this.Core.CreateRegistryRow(sourceLineNumbers, RegistryRootType.ClassesRoot, String.Concat(progId, "\\DefaultIcon"), String.Empty, icon, componentId);
                }
            }

            if (null != noOpen)
            {
                this.Core.CreateRegistryRow(sourceLineNumbers, RegistryRootType.ClassesRoot, progId, "NoOpen", noOpen, componentId); // ProgId NoOpen name
            }

            // raise an error for an orphaned ProgId
            if (YesNoType.Yes == advertise && !foundExtension && null == parent && null == classId)
            {
                this.Core.Write(WarningMessages.OrphanedProgId(sourceLineNumbers, progId));
            }

            return progId;
        }

        /// <summary>
        /// Parses a property element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        private void ParsePropertyElement(XElement node)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            var admin = false;
            var complianceCheck = false;
            var hidden = false;
            var secure = false;
            var suppressModularization = YesNoType.NotSet;
            string value = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Admin":
                        admin = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "ComplianceCheck":
                        complianceCheck = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "Hidden":
                        hidden = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "Secure":
                        secure = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "SuppressModularization":
                        suppressModularization = this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "Value":
                        value = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == id)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Id"));
                id = Identifier.Invalid;
            }
            else if ("ProductID" == id.Id)
            {
                this.Core.Write(WarningMessages.ProductIdAuthored(sourceLineNumbers));
            }
            else if ("SecureCustomProperties" == id.Id || "AdminProperties" == id.Id || "MsiHiddenProperties" == id.Id)
            {
                this.Core.Write(ErrorMessages.CannotAuthorSpecialProperties(sourceLineNumbers, id.Id));
            }

            var innerText = this.Core.GetTrimmedInnerText(node);
            if (null != value)
            {
                // cannot specify both the value attribute and inner text
                if (!String.IsNullOrEmpty(innerText))
                {
                    this.Core.Write(ErrorMessages.IllegalAttributeWithInnerText(sourceLineNumbers, node.Name.LocalName, "Value"));
                }
            }
            else // value attribute not specified, use inner text if any.
            {
                value = innerText;
            }

            if ("ErrorDialog" == id.Id)
            {
                this.Core.CreateSimpleReference(sourceLineNumbers, "Dialog", value);
            }

            foreach (var child in node.Elements())
            {
                if (CompilerCore.WixNamespace == child.Name.Namespace)
                {
                    {
                        switch (child.Name.LocalName)
                        {
                        case "ProductSearch":
                            this.ParseProductSearchElement(child, id.Id);
                            secure = true;
                            break;
                        default:
                            // let ParseSearchSignatures handle standard AppSearch children and unknown elements
                            break;
                        }
                    }
                }
            }

            // see if this property is used for appSearch
            var signatures = this.ParseSearchSignatures(node);

            // If we're doing CCP then there must be a signature.
            if (complianceCheck && 0 == signatures.Count)
            {
                this.Core.Write(ErrorMessages.SearchElementRequiredWithAttribute(sourceLineNumbers, node.Name.LocalName, "ComplianceCheck", "yes"));
            }

            foreach (var sig in signatures)
            {
                if (complianceCheck && !this.Core.EncounteredError)
                {
                    this.Core.AddTuple(new CCPSearchTuple(sourceLineNumbers, new Identifier(AccessModifier.Private, sig)));
                }

                this.AddAppSearch(sourceLineNumbers, id, sig);
            }

            // If we're doing AppSearch get that setup.
            if (0 < signatures.Count)
            {
                this.AddProperty(sourceLineNumbers, id, value, admin, secure, hidden, false);
            }
            else // just a normal old property.
            {
                // If the property value is empty and none of the flags are set, print out a warning that we're ignoring
                // the element.
                if (String.IsNullOrEmpty(value) && !admin && !secure && !hidden)
                {
                    this.Core.Write(WarningMessages.PropertyUseless(sourceLineNumbers, id.Id));
                }
                else // there is a value and/or a flag set, do that.
                {
                    this.AddProperty(sourceLineNumbers, id, value, admin, secure, hidden, false);
                }
            }

            if (!this.Core.EncounteredError && YesNoType.Yes == suppressModularization)
            {
                this.Core.Write(WarningMessages.PropertyModularizationSuppressed(sourceLineNumbers));

                this.Core.AddTuple(new WixSuppressModularizationTuple(sourceLineNumbers, id));
            }
        }

        /// <summary>
        /// Parses a RegistryKey element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="componentId">Identifier for parent component.</param>
        /// <param name="root">Root specified when element is nested under another Registry element, otherwise CompilerConstants.IntegerNotSet.</param>
        /// <param name="parentKey">Parent key for this Registry element when nested.</param>
        /// <param name="win64Component">true if the component is 64-bit.</param>
        /// <param name="possibleKeyPath">Identifier of this registry key since it could be the component's keypath.</param>
        /// <returns>Yes if this element was marked as the parent component's key path, No if explicitly marked as not being a key path, or NotSet otherwise.</returns>
        [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "Changing the way this string normalizes would result " +
                         "in a change to the way the Registry table is generated, potentially causing extra churn in patches on an MSI built from an older version of WiX. " +
                         "Furthermore, there is no security hole here, as the strings won't need to make a round trip")]
        private YesNoType ParseRegistryKeyElement(XElement node, string componentId, RegistryRootType? root, string parentKey, bool win64Component, out string possibleKeyPath)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            var key = parentKey; // default to parent key path
            var forceCreateOnInstall = false;
            var forceDeleteOnUninstall = false;
            var keyPath = YesNoType.NotSet;

            possibleKeyPath = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Action":
                        this.Core.Write(WarningMessages.DeprecatedRegistryKeyActionAttribute(sourceLineNumbers));
                        var actionValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (actionValue)
                        {
                        case "create":
                            forceCreateOnInstall = true;
                            break;
                        case "createAndRemoveOnUninstall":
                            forceCreateOnInstall = true;
                            forceDeleteOnUninstall = true;
                            break;
                        case "none":
                            break;
                        case "":
                            break;
                        default:
                            this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, actionValue, "create", "createAndRemoveOnUninstall", "none"));
                            break;
                        }
                        break;
                    case "ForceCreateOnInstall":
                        forceCreateOnInstall = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "ForceDeleteOnUninstall":
                        forceDeleteOnUninstall = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "Key":
                        key = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        if (null != parentKey)
                        {
                            key = Path.Combine(parentKey, key);
                        }
                        break;
                    case "Root":
                        if (root.HasValue)
                        {
                            this.Core.Write(ErrorMessages.RegistryRootInvalid(sourceLineNumbers));
                        }

                        root = this.Core.GetAttributeRegistryRootValue(sourceLineNumbers, attrib, true);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            var name = forceCreateOnInstall ? (forceDeleteOnUninstall ? "*" : "+") : (forceDeleteOnUninstall ? "-" : null);

            if (forceCreateOnInstall || forceDeleteOnUninstall) // generates a Registry row, so an Id must be present
            {
                // generate the identifier if it wasn't provided
                if (null == id)
                {
                    id = this.Core.CreateIdentifier("reg", componentId, ((int)root).ToString(CultureInfo.InvariantCulture.NumberFormat), LowercaseOrNull(key), LowercaseOrNull(name));
                }
            }
            else // does not generate a Registry row, so no Id should be present
            {
                if (null != id)
                {
                    this.Core.Write(ErrorMessages.IllegalAttributeWithoutOtherAttributes(sourceLineNumbers, node.Name.LocalName, "Id", "ForceCreateOnInstall", "ForceDeleteOnUninstall", "yes", true));
                }
            }

            if (!root.HasValue)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Root"));
            }

            if (null == key)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Key"));
                key = String.Empty; // set the key to something to prevent null reference exceptions
            }

            foreach (var child in node.Elements())
            {
                if (CompilerCore.WixNamespace == child.Name.Namespace)
                {
                    string possibleChildKeyPath = null;

                    switch (child.Name.LocalName)
                    {
                    case "RegistryKey":
                        if (YesNoType.Yes == this.ParseRegistryKeyElement(child, componentId, root, key, win64Component, out possibleChildKeyPath))
                        {
                            if (YesNoType.Yes == keyPath)
                            {
                                this.Core.Write(ErrorMessages.ComponentMultipleKeyPaths(sourceLineNumbers, child.Name.LocalName, "KeyPath", "yes", "File", "RegistryValue", "ODBCDataSource"));
                            }

                            possibleKeyPath = possibleChildKeyPath; // the child is the key path
                            keyPath = YesNoType.Yes;
                        }
                        else if (null == possibleKeyPath && null != possibleChildKeyPath)
                        {
                            possibleKeyPath = possibleChildKeyPath;
                        }
                        break;
                    case "RegistryValue":
                        if (YesNoType.Yes == this.ParseRegistryValueElement(child, componentId, root, key, win64Component, out possibleChildKeyPath))
                        {
                            if (YesNoType.Yes == keyPath)
                            {
                                this.Core.Write(ErrorMessages.ComponentMultipleKeyPaths(sourceLineNumbers, child.Name.LocalName, "KeyPath", "yes", "File", "RegistryValue", "ODBCDataSource"));
                            }

                            possibleKeyPath = possibleChildKeyPath; // the child is the key path
                            keyPath = YesNoType.Yes;
                        }
                        else if (null == possibleKeyPath && null != possibleChildKeyPath)
                        {
                            possibleKeyPath = possibleChildKeyPath;
                        }
                        break;
                    case "Permission":
                        if (!forceCreateOnInstall)
                        {
                            this.Core.Write(ErrorMessages.UnexpectedElementWithAttributeValue(sourceLineNumbers, node.Name.LocalName, child.Name.LocalName, "ForceCreateOnInstall", "yes"));
                        }
                        this.ParsePermissionElement(child, id.Id, "Registry");
                        break;
                    case "PermissionEx":
                        if (!forceCreateOnInstall)
                        {
                            this.Core.Write(ErrorMessages.UnexpectedElementWithAttributeValue(sourceLineNumbers, node.Name.LocalName, child.Name.LocalName, "ForceCreateOnInstall", "yes"));
                        }
                        this.ParsePermissionExElement(child, id.Id, "Registry");
                        break;
                    default:
                        this.Core.UnexpectedElement(node, child);
                        break;
                    }
                }
                else
                {
                    var context = new Dictionary<string, string>() { { "RegistryId", id.Id }, { "ComponentId", componentId }, { "Win64", win64Component.ToString() } };
                    this.Core.ParseExtensionElement(node, child, context);
                }
            }

            if (!this.Core.EncounteredError && null != name)
            {
                var tuple = new RegistryTuple(sourceLineNumbers, id)
                {
                    Root = root.Value,
                    Key = key,
                    Name = name,
                    ComponentRef = componentId,
                };

                this.Core.AddTuple(tuple);
            }

            return keyPath;
        }

        /// <summary>
        /// Parses a RegistryValue element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="componentId">Identifier for parent component.</param>
        /// <param name="root">Root specified when element is nested under a RegistryKey element, otherwise CompilerConstants.IntegerNotSet.</param>
        /// <param name="parentKey">Root specified when element is nested under a RegistryKey element, otherwise CompilerConstants.IntegerNotSet.</param>
        /// <param name="win64Component">true if the component is 64-bit.</param>
        /// <param name="possibleKeyPath">Identifier of this registry key since it could be the component's keypath.</param>
        /// <returns>Yes if this element was marked as the parent component's key path, No if explicitly marked as not being a key path, or NotSet otherwise.</returns>
        [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "Changing the way this string normalizes would result " +
                         "in a change to the way the Registry table is generated, potentially causing extra churn in patches on an MSI built from an older version of WiX. " +
                         "Furthermore, there is no security hole here, as the strings won't need to make a round trip")]
        private YesNoType ParseRegistryValueElement(XElement node, string componentId, RegistryRootType? root, string parentKey, bool win64Component, out string possibleKeyPath)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            var key = parentKey; // default to parent key path
            string name = null;
            string value = null;
            string action = null;
            var valueType = RegistryValueType.String;
            var actionType = RegistryValueActionType.Write;
            var keyPath = YesNoType.NotSet;

            possibleKeyPath = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Action":
                        var actionValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (actionValue)
                        {
                        case "append":
                            actionType = RegistryValueActionType.Append;
                            break;
                        case "prepend":
                            actionType = RegistryValueActionType.Prepend;
                            break;
                        case "write":
                            actionType = RegistryValueActionType.Write;
                            break;
                        case "":
                            break;
                        default:
                            this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, actionValue, "append", "prepend", "write"));
                            break;
                        }
                        break;
                    case "Key":
                        key = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        if (null != parentKey)
                        {
                            if (parentKey.EndsWith("\\", StringComparison.Ordinal))
                            {
                                key = String.Concat(parentKey, key);
                            }
                            else
                            {
                                key = String.Concat(parentKey, "\\", key);
                            }
                        }
                        break;
                    case "KeyPath":
                        keyPath = this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "Name":
                        name = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Root":
                        if (root.HasValue)
                        {
                            this.Core.Write(ErrorMessages.RegistryRootInvalid(sourceLineNumbers));
                        }

                        root = this.Core.GetAttributeRegistryRootValue(sourceLineNumbers, attrib, true);
                        break;
                    case "Type":
                        var typeValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (typeValue)
                        {
                        case "binary":
                            valueType = RegistryValueType.Binary;
                            break;
                        case "expandable":
                            valueType = RegistryValueType.Expandable;
                            break;
                        case "integer":
                            valueType = RegistryValueType.Integer;
                            break;
                        case "multiString":
                            valueType = RegistryValueType.MultiString;
                            break;
                        case "string":
                            valueType = RegistryValueType.String;
                            break;
                        case "":
                            break;
                        default:
                            this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, typeValue, "binary", "expandable", "integer", "multiString", "string"));
                            break;
                        }
                        break;
                    case "Value":
                        value = this.Core.GetAttributeValue(sourceLineNumbers, attrib, EmptyRule.CanBeEmpty);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            // generate the identifier if it wasn't provided
            if (null == id)
            {
                id = this.Core.CreateIdentifier("reg", componentId, ((int)(root ?? RegistryRootType.Unknown)).ToString(), LowercaseOrNull(key), LowercaseOrNull(name));
            }

            if (RegistryValueType.MultiString != valueType && (RegistryValueActionType.Append == actionType || RegistryValueActionType.Prepend == actionType))
            {
                this.Core.Write(ErrorMessages.IllegalAttributeValueWithoutOtherAttribute(sourceLineNumbers, node.Name.LocalName, "Action", action, "Type", "multiString"));
            }

            if (null == key)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Key"));
            }

            if (!root.HasValue)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Root"));
            }

            foreach (var child in node.Elements())
            {
                if (CompilerCore.WixNamespace == child.Name.Namespace)
                {
                    switch (child.Name.LocalName)
                    {
                    case "MultiStringValue":
                        if (RegistryValueType.MultiString != valueType && null != value)
                        {
                            this.Core.Write(ErrorMessages.RegistryMultipleValuesWithoutMultiString(sourceLineNumbers, node.Name.LocalName, "Value", child.Name.LocalName, "Type"));
                        }
                        else if (null == value)
                        {
                            value = Common.GetInnerText(child);
                        }
                        else
                        {
                            value = String.Concat(value, "[~]", Common.GetInnerText(child));
                        }
                        break;
                    case "Permission":
                        this.ParsePermissionElement(child, id.Id, "Registry");
                        break;
                    case "PermissionEx":
                        this.ParsePermissionExElement(child, id.Id, "Registry");
                        break;
                    default:
                        this.Core.UnexpectedElement(node, child);
                        break;
                    }
                }
                else
                {
                    var context = new Dictionary<string, string>() { { "RegistryId", id.Id }, { "ComponentId", componentId }, { "Win64", win64Component.ToString() } };
                    this.Core.ParseExtensionElement(node, child, context);
                }
            }

            //switch (typeType)
            //{
            //case Wix.RegistryValue.TypeType.binary:
            //    value = String.Concat("#x", value);
            //    break;
            //case Wix.RegistryValue.TypeType.expandable:
            //    value = String.Concat("#%", value);
            //    break;
            //case Wix.RegistryValue.TypeType.integer:
            //    value = String.Concat("#", value);
            //    break;
            //case Wix.RegistryValue.TypeType.multiString:
            //    switch (actionType)
            //    {
            //    case Wix.RegistryValue.ActionType.append:
            //        value = String.Concat("[~]", value);
            //        break;
            //    case Wix.RegistryValue.ActionType.prepend:
            //        value = String.Concat(value, "[~]");
            //        break;
            //    case Wix.RegistryValue.ActionType.write:
            //    default:
            //        if (null != value && -1 == value.IndexOf("[~]", StringComparison.Ordinal))
            //        {
            //            value = String.Format(CultureInfo.InvariantCulture, "[~]{0}[~]", value);
            //        }
            //        break;
            //    }
            //    break;
            //case Wix.RegistryValue.TypeType.@string:
            //    // escape the leading '#' character for string registry keys
            //    if (null != value && value.StartsWith("#", StringComparison.Ordinal))
            //    {
            //        value = String.Concat("#", value);
            //    }
            //    break;
            //}

            // value may be set by child MultiStringValue elements, so it must be checked here
            if (null == value)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Value"));
            }
            else if (0 == value.Length && ("+" == name || "-" == name || "*" == name)) // prevent accidental authoring of special name values
            {
                this.Core.Write(ErrorMessages.RegistryNameValueIncorrect(sourceLineNumbers, node.Name.LocalName, "Name", name));
            }

            if (!this.Core.EncounteredError)
            {
                var tuple = new RegistryTuple(sourceLineNumbers, id)
                {
                    Root = root.Value,
                    Name = name,
                    Value = value,
                    ValueType = valueType,
                    ValueAction = actionType,
                    ComponentRef = componentId,
                };

                this.Core.AddTuple(tuple);
                //var row = this.Core.CreateRow(sourceLineNumbers, TupleDefinitionType.Registry, id);
                //row.Set(1, (int)root);
                //row.Set(2, key);
                //row.Set(3, name);
                //row.Set(4, value);
                //row.Set(5, componentId);
            }

            // If this was just a regular registry key (that could be the key path)
            // and no child registry key set the possible key path, let's make this
            // Registry/@Id a possible key path.
            if (null == possibleKeyPath)
            {
                possibleKeyPath = id.Id;
            }

            return keyPath;
        }

        /// <summary>
        /// Parses a RemoveRegistryKey element.
        /// </summary>
        /// <param name="node">The element to parse.</param>
        /// <param name="componentId">The component identifier of the parent element.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "Changing the way this string normalizes would result " +
                         "in a change to the way the Registry table is generated, potentially causing extra churn in patches on an MSI built from an older version of WiX. " +
                         "Furthermore, there is no security hole here, as the strings won't need to make a round trip")]
        private void ParseRemoveRegistryKeyElement(XElement node, string componentId)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            RemoveRegistryActionType? actionType = null;
            string key = null;
            var name = "-";
            RegistryRootType? root = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Action":
                        var actionValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (actionValue)
                        {
                        case "removeOnInstall":
                            actionType = RemoveRegistryActionType.RemoveOnInstall;
                            break;
                        case "removeOnUninstall":
                            actionType = RemoveRegistryActionType.RemoveOnUninstall;
                            break;
                        case "":
                            break;
                        default:
                            this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, actionValue, "removeOnInstall", "removeOnUninstall"));
                            break;
                        }
                        //if (0 < action.Length)
                        //{
                        //    if (!Wix.RemoveRegistryKey.TryParseActionType(action, out actionType))
                        //    {
                        //        this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, action, "removeOnInstall", "removeOnUninstall"));
                        //    }
                        //}
                        break;
                    case "Key":
                        key = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Root":
                        root = this.Core.GetAttributeRegistryRootValue(sourceLineNumbers, attrib, true);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            // generate the identifier if it wasn't provided
            if (null == id)
            {
                id = this.Core.CreateIdentifier("reg", componentId, ((int)root).ToString(CultureInfo.InvariantCulture.NumberFormat), LowercaseOrNull(key), LowercaseOrNull(name));
            }

            if (!root.HasValue)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Root"));
            }

            if (null == key)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Key"));
            }

            if (!actionType.HasValue)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Action"));
            }

            this.Core.ParseForExtensionElements(node);

            if (!this.Core.EncounteredError)
            {
                var tuple = new RemoveRegistryTuple(sourceLineNumbers, id)
                {
                    Root = root.Value,
                    Key = key,
                    Name = name,
                    Action = actionType.Value,
                    ComponentRef = componentId
                };

                this.Core.AddTuple(tuple);
            }
        }

        /// <summary>
        /// Parses a RemoveRegistryValue element.
        /// </summary>
        /// <param name="node">The element to parse.</param>
        /// <param name="componentId">The component identifier of the parent element.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "Changing the way this string normalizes would result " +
                         "in a change to the way the Registry table is generated, potentially causing extra churn in patches on an MSI built from an older version of WiX. " +
                         "Furthermore, there is no security hole here, as the strings won't need to make a round trip")]
        private void ParseRemoveRegistryValueElement(XElement node, string componentId)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            string key = null;
            string name = null;
            RegistryRootType? root = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Key":
                        key = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Name":
                        name = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Root":
                        root = this.Core.GetAttributeRegistryRootValue(sourceLineNumbers, attrib, true);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            // generate the identifier if it wasn't provided
            if (null == id)
            {
                id = this.Core.CreateIdentifier("reg", componentId, ((int)root).ToString(CultureInfo.InvariantCulture.NumberFormat), LowercaseOrNull(key), LowercaseOrNull(name));
            }

            if (!root.HasValue)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Root"));
            }

            if (null == key)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Key"));
            }

            this.Core.ParseForExtensionElements(node);

            if (!this.Core.EncounteredError)
            {
                var tuple = new RemoveRegistryTuple(sourceLineNumbers, id)
                {
                    Root = root.Value,
                    Key = key,
                    Name = name,
                    ComponentRef = componentId
                };

                this.Core.AddTuple(tuple);
            }
        }

        /// <summary>
        /// Parses a remove file element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="componentId">Identifier of parent component.</param>
        /// <param name="parentDirectory">Identifier of the parent component's directory.</param>
        private void ParseRemoveFileElement(XElement node, string componentId, string parentDirectory)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            string directory = null;
            string name = null;
            bool? onInstall = null;
            bool? onUninstall = null;
            string property = null;
            string shortName = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Directory":
                        directory = this.Core.CreateDirectoryReferenceFromInlineSyntax(sourceLineNumbers, attrib, parentDirectory);
                        break;
                    case "Name":
                        name = this.Core.GetAttributeLongFilename(sourceLineNumbers, attrib, true);
                        break;
                    case "On":
                        var onValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (onValue)
                        {
                        case "install":
                            onInstall = true;
                            break;
                        case "uninstall":
                            onUninstall = true;
                            break;
                        case "both":
                            onInstall = true;
                            onUninstall = true;
                            break;
                        }
                        break;
                    case "Property":
                        property = this.Core.GetAttributeIdentifierValue(sourceLineNumbers, attrib);
                        break;
                    case "ShortName":
                        shortName = this.Core.GetAttributeShortFilename(sourceLineNumbers, attrib, true);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == name)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Name"));
            }
            else if (0 < name.Length)
            {
                if (this.Core.IsValidShortFilename(name, true))
                {
                    if (null == shortName)
                    {
                        shortName = name;
                        name = null;
                    }
                    else
                    {
                        this.Core.Write(ErrorMessages.IllegalAttributeValueWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "Name", name, "ShortName"));
                    }
                }
                else if (null == shortName) // generate a short file name.
                {
                    shortName = this.Core.CreateShortName(name, true, true, node.Name.LocalName, componentId);
                }
            }

            if (!onInstall.HasValue && !onUninstall.HasValue)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "On"));
            }

            if (null != directory && null != property)
            {
                this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "Property", "Directory", directory));
            }

            if (null == id)
            {
                var on = (onInstall == true && onUninstall == true) ? 3 : (onUninstall == true) ? 2 : (onInstall == true) ? 1 : 0;
                id = this.Core.CreateIdentifier("rmf", directory ?? property ?? parentDirectory, LowercaseOrNull(shortName), LowercaseOrNull(name), on.ToString());
            }

            this.Core.ParseForExtensionElements(node);

            if (!this.Core.EncounteredError)
            {
                var tuple = new RemoveFileTuple(sourceLineNumbers, id)
                {
                    ComponentRef = componentId,
                    FileName = this.GetMsiFilenameValue(shortName, name),
                    DirProperty = directory ?? property ?? parentDirectory,
                    OnInstall = onInstall,
                    OnUninstall = onUninstall
                };

                this.Core.AddTuple(tuple);
            }
        }

        /// <summary>
        /// Parses a RemoveFolder element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="componentId">Identifier of parent component.</param>
        /// <param name="parentDirectory">Identifier of parent component's directory.</param>
        private void ParseRemoveFolderElement(XElement node, string componentId, string parentDirectory)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            string directory = null;
            bool? onInstall = null;
            bool? onUninstall = null;
            string property = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Directory":
                        directory = this.Core.CreateDirectoryReferenceFromInlineSyntax(sourceLineNumbers, attrib, parentDirectory);
                        break;
                    case "On":
                        var onValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (onValue)
                        {
                        case "install":
                            onInstall = true;
                            break;
                        case "uninstall":
                            onUninstall = true;
                            break;
                        case "both":
                            onInstall = true;
                            onUninstall = true;
                            break;
                        }
                        break;
                    case "Property":
                        property = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (!onInstall.HasValue && !onUninstall.HasValue)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "On"));
            }

            if (null != directory && null != property)
            {
                this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "Property", "Directory", directory));
            }

            if (null == id)
            {
                var on = (onInstall == true && onUninstall == true) ? 3 : (onUninstall == true) ? 2 : (onInstall == true) ? 1 : 0;
                id = this.Core.CreateIdentifier("rmf", directory ?? property ?? parentDirectory, on.ToString());
            }

            this.Core.ParseForExtensionElements(node);

            if (!this.Core.EncounteredError)
            {
                var tuple = new RemoveFileTuple(sourceLineNumbers, id)
                {
                    ComponentRef = componentId,
                    DirProperty = directory ?? property ?? parentDirectory,
                    OnInstall = onInstall,
                    OnUninstall = onUninstall
                };

                this.Core.AddTuple(tuple);
            }
        }

        /// <summary>
        /// Parses a reserve cost element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="componentId">Identifier of parent component.</param>
        /// <param name="directoryId">Optional and default identifier of referenced directory.</param>
        private void ParseReserveCostElement(XElement node, string componentId, string directoryId)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            var runFromSource = CompilerConstants.IntegerNotSet;
            var runLocal = CompilerConstants.IntegerNotSet;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Directory":
                        directoryId = this.Core.CreateDirectoryReferenceFromInlineSyntax(sourceLineNumbers, attrib, directoryId);
                        break;
                    case "RunFromSource":
                        runFromSource = this.Core.GetAttributeIntegerValue(sourceLineNumbers, attrib, 0, Int32.MaxValue);
                        break;
                    case "RunLocal":
                        runLocal = this.Core.GetAttributeIntegerValue(sourceLineNumbers, attrib, 0, Int32.MaxValue);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == id)
            {
                id = this.Core.CreateIdentifier("rc", componentId, directoryId);
            }

            if (CompilerConstants.IntegerNotSet == runFromSource)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "RunFromSource"));
            }

            if (CompilerConstants.IntegerNotSet == runLocal)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "RunLocal"));
            }

            this.Core.ParseForExtensionElements(node);

            if (!this.Core.EncounteredError)
            {
                this.Core.AddTuple(new ReserveCostTuple(sourceLineNumbers, id)
                {
                    ComponentRef = componentId,
                    ReserveFolder = directoryId,
                    ReserveLocal = runLocal,
                    ReserveSource = runFromSource
                });
            }
        }

        /// <summary>
        /// Parses a sequence element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="sequenceTable">Name of sequence table.</param>
        private void ParseSequenceElement(XElement node, SequenceTable sequenceTable)
        {
            // Parse each action in the sequence.
            foreach (var child in node.Elements())
            {
                var childSourceLineNumbers = Preprocessor.GetSourceLineNumbers(child);
                var actionName = child.Name.LocalName;
                string afterAction = null;
                string beforeAction = null;
                string condition = null;
                var customAction = "Custom" == actionName;
                var overridable = false;
                var exitSequence = CompilerConstants.IntegerNotSet;
                var sequence = CompilerConstants.IntegerNotSet;
                var showDialog = "Show" == actionName;
                var specialAction = "InstallExecute" == actionName || "InstallExecuteAgain" == actionName || "RemoveExistingProducts" == actionName || "DisableRollback" == actionName || "ScheduleReboot" == actionName || "ForceReboot" == actionName || "ResolveSource" == actionName;
                var specialStandardAction = "AppSearch" == actionName || "CCPSearch" == actionName || "RMCCPSearch" == actionName || "LaunchConditions" == actionName || "FindRelatedProducts" == actionName;
                var suppress = false;

                foreach (var attrib in child.Attributes())
                {
                    if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                    {
                        switch (attrib.Name.LocalName)
                        {
                        case "Action":
                            if (customAction)
                            {
                                actionName = this.Core.GetAttributeIdentifierValue(childSourceLineNumbers, attrib);
                                this.Core.CreateSimpleReference(childSourceLineNumbers, "CustomAction", actionName);
                            }
                            else
                            {
                                this.Core.UnexpectedAttribute(child, attrib);
                            }
                            break;
                        case "After":
                            if (customAction || showDialog || specialAction || specialStandardAction)
                            {
                                afterAction = this.Core.GetAttributeIdentifierValue(childSourceLineNumbers, attrib);
                                this.Core.CreateSimpleReference(childSourceLineNumbers, "WixAction", sequenceTable.ToString(), afterAction);
                            }
                            else
                            {
                                this.Core.UnexpectedAttribute(child, attrib);
                            }
                            break;
                        case "Before":
                            if (customAction || showDialog || specialAction || specialStandardAction)
                            {
                                beforeAction = this.Core.GetAttributeIdentifierValue(childSourceLineNumbers, attrib);
                                this.Core.CreateSimpleReference(childSourceLineNumbers, "WixAction", sequenceTable.ToString(), beforeAction);
                            }
                            else
                            {
                                this.Core.UnexpectedAttribute(child, attrib);
                            }
                            break;
                        case "Dialog":
                            if (showDialog)
                            {
                                actionName = this.Core.GetAttributeIdentifierValue(childSourceLineNumbers, attrib);
                                this.Core.CreateSimpleReference(childSourceLineNumbers, "Dialog", actionName);
                            }
                            else
                            {
                                this.Core.UnexpectedAttribute(child, attrib);
                            }
                            break;
                        case "OnExit":
                            if (customAction || showDialog || specialAction)
                            {
                                var exitValue = this.Core.GetAttributeValue(childSourceLineNumbers, attrib);
                                switch (exitValue)
                                {
                                case "success":
                                    exitSequence = -1;
                                    break;
                                case "cancel":
                                    exitSequence = -2;
                                    break;
                                case "error":
                                    exitSequence = -3;
                                    break;
                                case "suspend":
                                    exitSequence = -4;
                                    break;
                                }
                            }
                            else
                            {
                                this.Core.UnexpectedAttribute(child, attrib);
                            }
                            break;
                        case "Overridable":
                            overridable = YesNoType.Yes == this.Core.GetAttributeYesNoValue(childSourceLineNumbers, attrib);
                            break;
                        case "Sequence":
                            sequence = this.Core.GetAttributeIntegerValue(childSourceLineNumbers, attrib, 1, Int16.MaxValue);
                            break;
                        case "Suppress":
                            suppress = YesNoType.Yes == this.Core.GetAttributeYesNoValue(childSourceLineNumbers, attrib);
                            break;
                        default:
                            this.Core.UnexpectedAttribute(node, attrib);
                            break;
                        }
                    }
                    else
                    {
                        this.Core.ParseExtensionAttribute(node, attrib);
                    }
                }

                // Get the condition from the inner text of the element.
                condition = this.Core.GetConditionInnerText(child);

                if (customAction && "Custom" == actionName)
                {
                    this.Core.Write(ErrorMessages.ExpectedAttribute(childSourceLineNumbers, child.Name.LocalName, "Action"));
                }
                else if (showDialog && "Show" == actionName)
                {
                    this.Core.Write(ErrorMessages.ExpectedAttribute(childSourceLineNumbers, child.Name.LocalName, "Dialog"));
                }

                if (CompilerConstants.IntegerNotSet != sequence)
                {
                    if (CompilerConstants.IntegerNotSet != exitSequence)
                    {
                        this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(childSourceLineNumbers, child.Name.LocalName, "Sequence", "OnExit"));
                    }
                    else if (null != beforeAction || null != afterAction)
                    {
                        this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(childSourceLineNumbers, child.Name.LocalName, "Sequence", "Before", "After"));
                    }
                }
                else // sequence not specified use OnExit (which may also be not set).
                {
                    sequence = exitSequence;
                }

                if (null != beforeAction && null != afterAction)
                {
                    this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(childSourceLineNumbers, child.Name.LocalName, "After", "Before"));
                }
                else if ((customAction || showDialog || specialAction) && !suppress && CompilerConstants.IntegerNotSet == sequence && null == beforeAction && null == afterAction)
                {
                    this.Core.Write(ErrorMessages.NeedSequenceBeforeOrAfter(childSourceLineNumbers, child.Name.LocalName));
                }

                // action that is scheduled to occur before/after itself
                if (beforeAction == actionName)
                {
                    this.Core.Write(ErrorMessages.ActionScheduledRelativeToItself(childSourceLineNumbers, child.Name.LocalName, "Before", beforeAction));
                }
                else if (afterAction == actionName)
                {
                    this.Core.Write(ErrorMessages.ActionScheduledRelativeToItself(childSourceLineNumbers, child.Name.LocalName, "After", afterAction));
                }

                // normal standard actions cannot be set overridable by the user (since they are overridable by default)
                if (overridable && WindowsInstallerStandard.IsStandardAction(actionName) && !specialAction)
                {
                    this.Core.Write(ErrorMessages.UnexpectedAttribute(childSourceLineNumbers, child.Name.LocalName, "Overridable"));
                }

                // suppress cannot be specified at the same time as Before, After, or Sequence
                if (suppress && (null != afterAction || null != beforeAction || CompilerConstants.IntegerNotSet != sequence || overridable))
                {
                    this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttributes(childSourceLineNumbers, child.Name.LocalName, "Suppress", "Before", "After", "Sequence", "Overridable"));
                }

                this.Core.ParseForExtensionElements(child);

                // add the row and any references needed
                if (!this.Core.EncounteredError)
                {
                    if (suppress)
                    {
                        this.Core.AddTuple(new WixSuppressActionTuple(childSourceLineNumbers, new Identifier(AccessModifier.Public, sequenceTable, actionName))
                        {
                            SequenceTable = sequenceTable,
                            Action = actionName
                        });
                    }
                    else
                    {
                        var tuple = new WixActionTuple(childSourceLineNumbers, new Identifier(AccessModifier.Public, sequenceTable, actionName))
                        {
                            SequenceTable = sequenceTable,
                            Action = actionName,
                            Condition = condition,
                            Before = beforeAction,
                            After = afterAction,
                            Overridable = overridable,
                        };

                        if (CompilerConstants.IntegerNotSet != sequence)
                        {
                            tuple.Sequence = sequence;
                        }

                        this.Core.AddTuple(tuple);
                    }
                }
            }
        }


        /// <summary>
        /// Parses a service config element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="componentId">Identifier of parent component.</param>
        /// <param name="serviceName">Optional element containing parent's service name.</param>
        private void ParseServiceConfigElement(XElement node, string componentId, string serviceName)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            string delayedAutoStart = null;
            string failureActionsWhen = null;
            var name = serviceName;
            var install = false;
            var reinstall = false;
            var uninstall = false;
            string preShutdownDelay = null;
            string requiredPrivileges = null;
            string sid = null;

            this.Core.Write(WarningMessages.ServiceConfigFamilyNotSupported(sourceLineNumbers, node.Name.LocalName));

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "DelayedAutoStart":
                        delayedAutoStart = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (delayedAutoStart)
                        {
                        case "no":
                            delayedAutoStart = "0";
                            break;
                        case "yes":
                            delayedAutoStart = "1";
                            break;
                        default:
                            // allow everything else to pass through that are hopefully "formatted" Properties.
                            break;
                        }
                        break;
                    case "FailureActionsWhen":
                        failureActionsWhen = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (failureActionsWhen)
                        {
                        case "failedToStop":
                            failureActionsWhen = "0";
                            break;
                        case "failedToStopOrReturnedError":
                            failureActionsWhen = "1";
                            break;
                        default:
                            // allow everything else to pass through that are hopefully "formatted" Properties.
                            break;
                        }
                        break;
                    case "OnInstall":
                        install = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        //if (YesNoType.Yes == install)
                        //{
                        //    events |= MsiInterop.MsidbServiceConfigEventInstall;
                        //}
                        break;
                    case "OnReinstall":
                        reinstall = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        //if (YesNoType.Yes == reinstall)
                        //{
                        //    events |= MsiInterop.MsidbServiceConfigEventReinstall;
                        //}
                        break;
                    case "OnUninstall":
                        uninstall = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        //if (YesNoType.Yes == uninstall)
                        //{
                        //    events |= MsiInterop.MsidbServiceConfigEventUninstall;
                        //}
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    case "PreShutdownDelay":
                        preShutdownDelay = this.Core.GetAttributeValue(sourceLineNumbers, attrib, EmptyRule.CanBeEmpty);
                        break;
                    case "ServiceName":
                        if (!String.IsNullOrEmpty(serviceName))
                        {
                            this.Core.Write(ErrorMessages.IllegalAttributeWhenNested(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, "ServiceInstall"));
                        }

                        name = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "ServiceSid":
                        sid = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (sid)
                        {
                        case "none":
                            sid = "0";
                            break;
                        case "restricted":
                            sid = "3";
                            break;
                        case "unrestricted":
                            sid = "1";
                            break;
                        default:
                            // allow everything else to pass through that are hopefully "formatted" Properties.
                            break;
                        }
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            // Get the ServiceConfig required privilegs.
            foreach (var child in node.Elements())
            {
                if (CompilerCore.WixNamespace == child.Name.Namespace)
                {
                    switch (child.Name.LocalName)
                    {
                    case "RequiredPrivilege":
                        var privilege = this.Core.GetTrimmedInnerText(child);
                        switch (privilege)
                        {
                        case "assignPrimaryToken":
                            privilege = "SeAssignPrimaryTokenPrivilege";
                            break;
                        case "audit":
                            privilege = "SeAuditPrivilege";
                            break;
                        case "backup":
                            privilege = "SeBackupPrivilege";
                            break;
                        case "changeNotify":
                            privilege = "SeChangeNotifyPrivilege";
                            break;
                        case "createGlobal":
                            privilege = "SeCreateGlobalPrivilege";
                            break;
                        case "createPagefile":
                            privilege = "SeCreatePagefilePrivilege";
                            break;
                        case "createPermanent":
                            privilege = "SeCreatePermanentPrivilege";
                            break;
                        case "createSymbolicLink":
                            privilege = "SeCreateSymbolicLinkPrivilege";
                            break;
                        case "createToken":
                            privilege = "SeCreateTokenPrivilege";
                            break;
                        case "debug":
                            privilege = "SeDebugPrivilege";
                            break;
                        case "enableDelegation":
                            privilege = "SeEnableDelegationPrivilege";
                            break;
                        case "impersonate":
                            privilege = "SeImpersonatePrivilege";
                            break;
                        case "increaseBasePriority":
                            privilege = "SeIncreaseBasePriorityPrivilege";
                            break;
                        case "increaseQuota":
                            privilege = "SeIncreaseQuotaPrivilege";
                            break;
                        case "increaseWorkingSet":
                            privilege = "SeIncreaseWorkingSetPrivilege";
                            break;
                        case "loadDriver":
                            privilege = "SeLoadDriverPrivilege";
                            break;
                        case "lockMemory":
                            privilege = "SeLockMemoryPrivilege";
                            break;
                        case "machineAccount":
                            privilege = "SeMachineAccountPrivilege";
                            break;
                        case "manageVolume":
                            privilege = "SeManageVolumePrivilege";
                            break;
                        case "profileSingleProcess":
                            privilege = "SeProfileSingleProcessPrivilege";
                            break;
                        case "relabel":
                            privilege = "SeRelabelPrivilege";
                            break;
                        case "remoteShutdown":
                            privilege = "SeRemoteShutdownPrivilege";
                            break;
                        case "restore":
                            privilege = "SeRestorePrivilege";
                            break;
                        case "security":
                            privilege = "SeSecurityPrivilege";
                            break;
                        case "shutdown":
                            privilege = "SeShutdownPrivilege";
                            break;
                        case "syncAgent":
                            privilege = "SeSyncAgentPrivilege";
                            break;
                        case "systemEnvironment":
                            privilege = "SeSystemEnvironmentPrivilege";
                            break;
                        case "systemProfile":
                            privilege = "SeSystemProfilePrivilege";
                            break;
                        case "systemTime":
                        case "modifySystemTime":
                            privilege = "SeSystemtimePrivilege";
                            break;
                        case "takeOwnership":
                            privilege = "SeTakeOwnershipPrivilege";
                            break;
                        case "tcb":
                        case "trustedComputerBase":
                            privilege = "SeTcbPrivilege";
                            break;
                        case "timeZone":
                        case "modifyTimeZone":
                            privilege = "SeTimeZonePrivilege";
                            break;
                        case "trustedCredManAccess":
                        case "trustedCredentialManagerAccess":
                            privilege = "SeTrustedCredManAccessPrivilege";
                            break;
                        case "undock":
                            privilege = "SeUndockPrivilege";
                            break;
                        case "unsolicitedInput":
                            privilege = "SeUnsolicitedInputPrivilege";
                            break;
                        default:
                            // allow everything else to pass through that are hopefully "formatted" Properties.
                            break;
                        }

                        if (null != requiredPrivileges)
                        {
                            requiredPrivileges = String.Concat(requiredPrivileges, "[~]");
                        }
                        requiredPrivileges = String.Concat(requiredPrivileges, privilege);
                        break;
                    default:
                        this.Core.UnexpectedElement(node, child);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionElement(node, child);
                }
            }

            if (String.IsNullOrEmpty(name))
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "ServiceName"));
            }
            else if (null == id)
            {
                id = this.Core.CreateIdentifierFromFilename(name);
            }

            if (!install && !reinstall && !uninstall)
            {
                this.Core.Write(ErrorMessages.ExpectedAttributes(sourceLineNumbers, node.Name.LocalName, "OnInstall", "OnReinstall", "OnUninstall"));
            }

            if (String.IsNullOrEmpty(delayedAutoStart) && String.IsNullOrEmpty(failureActionsWhen) && String.IsNullOrEmpty(preShutdownDelay) && String.IsNullOrEmpty(requiredPrivileges) && String.IsNullOrEmpty(sid))
            {
                this.Core.Write(ErrorMessages.ExpectedAttributes(sourceLineNumbers, node.Name.LocalName, "DelayedAutoStart", "FailureActionsWhen", "PreShutdownDelay", "ServiceSid", "RequiredPrivilege"));
            }

            if (!this.Core.EncounteredError)
            {
                if (!String.IsNullOrEmpty(delayedAutoStart))
                {
                    var tuple = new MsiServiceConfigTuple(sourceLineNumbers, new Identifier(id.Access, String.Concat(id.Id, ".DS")))
                    {
                        Name = name,
                        OnInstall = install,
                        OnReinstall = reinstall,
                        OnUninstall = uninstall,
                        ConfigType = MsiServiceConfigType.DelayedAutoStart,
                        Argument = delayedAutoStart,
                        ComponentRef = componentId,
                    };

                    this.Core.AddTuple(tuple);
                    //var row = this.Core.CreateRow(sourceLineNumbers, TupleDefinitionType.MsiServiceConfig, new Identifier(String.Concat(id.Id, ".DS"), id.Access));
                    //row.Set(1, name);
                    //row.Set(2, events);
                    //row.Set(3, 3);
                    //row.Set(4, delayedAutoStart);
                    //row.Set(5, componentId);
                }

                if (!String.IsNullOrEmpty(failureActionsWhen))
                {
                    var tuple = new MsiServiceConfigTuple(sourceLineNumbers, new Identifier(id.Access, String.Concat(id.Id, ".FA")))
                    {
                        Name = name,
                        OnInstall = install,
                        OnReinstall = reinstall,
                        OnUninstall = uninstall,
                        ConfigType = MsiServiceConfigType.FailureActionsFlag,
                        Argument = failureActionsWhen,
                        ComponentRef = componentId,
                    };

                    this.Core.AddTuple(tuple);
                    //var row = this.Core.CreateRow(sourceLineNumbers, TupleDefinitionType.MsiServiceConfig, new Identifier(String.Concat(id.Id, ".FA"), id.Access));
                    //row.Set(1, name);
                    //row.Set(2, events);
                    //row.Set(3, 4);
                    //row.Set(4, failureActionsWhen);
                    //row.Set(5, componentId);
                }

                if (!String.IsNullOrEmpty(sid))
                {
                    var tuple = new MsiServiceConfigTuple(sourceLineNumbers, new Identifier(id.Access, String.Concat(id.Id, ".SS")))
                    {
                        Name = name,
                        OnInstall = install,
                        OnReinstall = reinstall,
                        OnUninstall = uninstall,
                        ConfigType = MsiServiceConfigType.ServiceSidInfo,
                        Argument = sid,
                        ComponentRef = componentId,
                    };

                    this.Core.AddTuple(tuple);
                    //var row = this.Core.CreateRow(sourceLineNumbers, TupleDefinitionType.MsiServiceConfig, new Identifier(String.Concat(id.Id, ".SS"), id.Access));
                    //row.Set(1, name);
                    //row.Set(2, events);
                    //row.Set(3, 5);
                    //row.Set(4, sid);
                    //row.Set(5, componentId);
                }

                if (!String.IsNullOrEmpty(requiredPrivileges))
                {
                    var tuple = new MsiServiceConfigTuple(sourceLineNumbers, new Identifier(id.Access, String.Concat(id.Id, ".RP")))
                    {
                        Name = name,
                        OnInstall = install,
                        OnReinstall = reinstall,
                        OnUninstall = uninstall,
                        ConfigType = MsiServiceConfigType.RequiredPrivilegesInfo,
                        Argument = requiredPrivileges,
                        ComponentRef = componentId,
                    };

                    this.Core.AddTuple(tuple);
                    //var row = this.Core.CreateRow(sourceLineNumbers, TupleDefinitionType.MsiServiceConfig, new Identifier(String.Concat(id.Id, ".RP"), id.Access));
                    //row.Set(1, name);
                    //row.Set(2, events);
                    //row.Set(3, 6);
                    //row.Set(4, requiredPrivileges);
                    //row.Set(5, componentId);
                }

                if (!String.IsNullOrEmpty(preShutdownDelay))
                {
                    var tuple = new MsiServiceConfigTuple(sourceLineNumbers, new Identifier(id.Access, String.Concat(id.Id, ".PD")))
                    {
                        Name = name,
                        OnInstall = install,
                        OnReinstall = reinstall,
                        OnUninstall = uninstall,
                        ConfigType = MsiServiceConfigType.PreshutdownInfo,
                        Argument = preShutdownDelay,
                        ComponentRef = componentId,
                    };

                    this.Core.AddTuple(tuple);
                    //var row = this.Core.CreateRow(sourceLineNumbers, TupleDefinitionType.MsiServiceConfig, new Identifier(String.Concat(id.Id, ".PD"), id.Access));
                    //row.Set(1, name);
                    //row.Set(2, events);
                    //row.Set(3, 7);
                    //row.Set(4, preShutdownDelay);
                    //row.Set(5, componentId);
                }
            }
        }

        /// <summary>
        /// Parses a service config failure actions element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="componentId">Identifier of parent component.</param>
        /// <param name="serviceName">Optional element containing parent's service name.</param>
        private void ParseServiceConfigFailureActionsElement(XElement node, string componentId, string serviceName)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            var name = serviceName;
            var install = false;
            var reinstall = false;
            var uninstall = false;
            int? resetPeriod = null;
            string rebootMessage = null;
            string command = null;
            string actions = null;
            string actionsDelays = null;

            this.Core.Write(WarningMessages.ServiceConfigFamilyNotSupported(sourceLineNumbers, node.Name.LocalName));

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Command":
                        command = this.Core.GetAttributeValue(sourceLineNumbers, attrib, EmptyRule.CanBeEmpty);
                        break;
                    case "OnInstall":
                        install = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "OnReinstall":
                        reinstall = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "OnUninstall":
                        uninstall = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "RebootMessage":
                        rebootMessage = this.Core.GetAttributeValue(sourceLineNumbers, attrib, EmptyRule.CanBeEmpty);
                        break;
                    case "ResetPeriod":
                        resetPeriod = this.Core.GetAttributeIntegerValue(sourceLineNumbers, attrib, 0, Int32.MaxValue);
                        break;
                    case "ServiceName":
                        if (!String.IsNullOrEmpty(serviceName))
                        {
                            this.Core.Write(ErrorMessages.IllegalAttributeWhenNested(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, "ServiceInstall"));
                        }

                        name = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            // Get the ServiceConfigFailureActions actions.
            foreach (var child in node.Elements())
            {
                if (CompilerCore.WixNamespace == child.Name.Namespace)
                {
                    switch (child.Name.LocalName)
                    {
                    case "Failure":
                        string action = null;
                        string delay = null;
                        var childSourceLineNumbers = Preprocessor.GetSourceLineNumbers(child);

                        foreach (var childAttrib in child.Attributes())
                        {
                            if (String.IsNullOrEmpty(childAttrib.Name.NamespaceName) || CompilerCore.WixNamespace == childAttrib.Name.Namespace)
                            {
                                switch (childAttrib.Name.LocalName)
                                {
                                case "Action":
                                    action = this.Core.GetAttributeValue(childSourceLineNumbers, childAttrib);
                                    switch (action)
                                    {
                                    case "none":
                                        action = "0";
                                        break;
                                    case "restartComputer":
                                        action = "2";
                                        break;
                                    case "restartService":
                                        action = "1";
                                        break;
                                    case "runCommand":
                                        action = "3";
                                        break;
                                    default:
                                        // allow everything else to pass through that are hopefully "formatted" Properties.
                                        break;
                                    }
                                    break;
                                case "Delay":
                                    delay = this.Core.GetAttributeValue(childSourceLineNumbers, childAttrib);
                                    break;
                                default:
                                    this.Core.UnexpectedAttribute(child, childAttrib);
                                    break;
                                }
                            }
                        }

                        if (String.IsNullOrEmpty(action))
                        {
                            this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, child.Name.LocalName, "Action"));
                        }

                        if (String.IsNullOrEmpty(delay))
                        {
                            this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, child.Name.LocalName, "Delay"));
                        }

                        if (!String.IsNullOrEmpty(actions))
                        {
                            actions = String.Concat(actions, "[~]");
                        }
                        actions = String.Concat(actions, action);

                        if (!String.IsNullOrEmpty(actionsDelays))
                        {
                            actionsDelays = String.Concat(actionsDelays, "[~]");
                        }
                        actionsDelays = String.Concat(actionsDelays, delay);
                        break;
                    default:
                        this.Core.UnexpectedElement(node, child);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionElement(node, child);
                }
            }

            if (String.IsNullOrEmpty(name))
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "ServiceName"));
            }
            else if (null == id)
            {
                id = this.Core.CreateIdentifierFromFilename(name);
            }

            if (!install && !reinstall && !uninstall)
            {
                this.Core.Write(ErrorMessages.ExpectedAttributes(sourceLineNumbers, node.Name.LocalName, "OnInstall", "OnReinstall", "OnUninstall"));
            }

            if (!this.Core.EncounteredError)
            {
                var tuple = new MsiServiceConfigFailureActionsTuple(sourceLineNumbers, id)
                {
                    Name = name,
                    OnInstall = install, 
                    OnReinstall = reinstall, 
                    OnUninstall = uninstall, 
                    ResetPeriod = resetPeriod,
                    RebootMessage = rebootMessage,
                    Command = command,
                    Actions = actions,
                    DelayActions = actionsDelays,
                    ComponentRef = componentId,
                };

                this.Core.AddTuple(tuple);
            }
        }

        /// <summary>
        /// Parses a service control element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="componentId">Identifier of parent component.</param>
        private void ParseServiceControlElement(XElement node, string componentId)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string arguments = null;
            Identifier id = null;
            string name = null;
            var installRemove = false;
            var uninstallRemove = false;
            var installStart = false;
            var uninstallStart = false;
            var installStop = false;
            var uninstallStop = false;
            bool? wait = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Name":
                        name = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Remove":
                        var removeValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (removeValue)
                        {
                        case "install":
                            installRemove = true;
                            break;
                        case "uninstall":
                            uninstallRemove = true;
                            break;
                        case "both":
                            installRemove = true;
                            uninstallRemove = true;
                            break;
                        case "":
                            break;
                        }
                        break;
                    case "Start":
                        var startValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (startValue)
                        {
                        case "install":
                            installStart = true;
                            break;
                        case "uninstall":
                            uninstallStart = true;
                            break;
                        case "both":
                            installStart = true;
                            uninstallStart = true;
                            break;
                        case "":
                            break;
                        }
                        break;
                    case "Stop":
                        var stopValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (stopValue)
                        {
                        case "install":
                            installStop = true;
                            break;
                        case "uninstall":
                            uninstallStop = true;
                            break;
                        case "both":
                            installStop = true;
                            uninstallStop = true;
                            break;
                        case "":
                            break;
                        }
                        break;
                    case "Wait":
                        wait = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == id)
            {
                id = this.Core.CreateIdentifierFromFilename(name);
            }

            if (null == name)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Name"));
            }

            // get the ServiceControl arguments
            foreach (var child in node.Elements())
            {
                if (CompilerCore.WixNamespace == child.Name.Namespace)
                {
                    switch (child.Name.LocalName)
                    {
                    case "ServiceArgument":
                        if (null != arguments)
                        {
                            arguments = String.Concat(arguments, "[~]");
                        }
                        arguments = String.Concat(arguments, this.Core.GetTrimmedInnerText(child));
                        break;
                    default:
                        this.Core.UnexpectedElement(node, child);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionElement(node, child);
                }
            }

            if (!this.Core.EncounteredError)
            {
                var tuple = new ServiceControlTuple(sourceLineNumbers, id)
                {
                    Name = name,
                    InstallRemove = installRemove,
                    UninstallRemove = uninstallRemove,
                    InstallStart = installStart,
                    UninstallStart = uninstallStart,
                    InstallStop = installStop,
                    UninstallStop = uninstallStop,
                    Arguments = arguments,
                    Wait = wait,
                    ComponentRef = componentId
                };

                this.Core.AddTuple(tuple);
            }
        }

        /// <summary>
        /// Parses a service dependency element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <returns>Parsed sevice dependency name.</returns>
        private string ParseServiceDependencyElement(XElement node)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string dependency = null;
            var group = false;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        dependency = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Group":
                        group = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == dependency)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Id"));
            }

            this.Core.ParseForExtensionElements(node);

            return group ? String.Concat("+", dependency) : dependency;
        }

        /// <summary>
        /// Parses a service install element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="componentId">Identifier of parent component.</param>
        private void ParseServiceInstallElement(XElement node, string componentId, bool win64Component)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            string account = null;
            string arguments = null;
            string dependencies = null;
            string description = null;
            string displayName = null;
            var eraseDescription = false;
            string loadOrderGroup = null;
            string name = null;
            string password = null;

            var serviceType = ServiceType.OwnProcess;
            var startType = ServiceStartType.Demand;
            var errorControl = ServiceErrorControl.Normal;
            var interactive = false;
            var vital = false;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Account":
                        account = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Arguments":
                        arguments = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Description":
                        description = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "DisplayName":
                        displayName = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "EraseDescription":
                        eraseDescription = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "ErrorControl":
                        var errorControlValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (errorControlValue)
                        {
                        case "ignore":
                            errorControl = ServiceErrorControl.Ignore;
                            break;
                        case "normal":
                            errorControl = ServiceErrorControl.Normal;
                            break;
                        case "critical":
                            errorControl = ServiceErrorControl.Critical;
                            break;
                        case "": // error case handled by GetAttributeValue()
                            break;
                        default:
                            this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, errorControlValue, "ignore", "normal", "critical"));
                            break;
                        }
                        break;
                    case "Interactive":
                        interactive = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "LoadOrderGroup":
                        loadOrderGroup = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Name":
                        name = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Password":
                        password = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Start":
                        var startValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (startValue)
                        {
                        case "auto":
                            startType = ServiceStartType.Auto;
                            break;
                        case "demand":
                            startType = ServiceStartType.Demand;
                            break;
                        case "disabled":
                            startType = ServiceStartType.Disabled;
                            break;
                        case "boot":
                        case "system":
                            this.Core.Write(ErrorMessages.ValueNotSupported(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, startValue));
                            break;
                        case "":
                            break;
                        default:
                            this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, startValue, "auto", "demand", "disabled"));
                            break;
                        }
                        break;
                    case "Type":
                        var typeValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (typeValue)
                        {
                            case "ownProcess":
                                serviceType = ServiceType.OwnProcess;
                                break;
                            case "shareProcess":
                                serviceType = ServiceType.ShareProcess;
                                break;
                            case "kernelDriver":
                            case "systemDriver":
                                this.Core.Write(ErrorMessages.ValueNotSupported(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, typeValue));
                                break;
                            case "":
                                break;
                            default:
                                this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, node.Name.LocalName, typeValue, "ownProcess", "shareProcess"));
                                break;
                        }
                        break;
                    case "Vital":
                        vital = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (String.IsNullOrEmpty(name))
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Name"));
            }
            else if (null == id)
            {
                id = this.Core.CreateIdentifierFromFilename(name);
            }

            if (0 == startType)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Start"));
            }

            if (eraseDescription)
            {
                description = "[~]";
            }

            // get the ServiceInstall dependencies and config
            foreach (var child in node.Elements())
            {
                if (CompilerCore.WixNamespace == child.Name.Namespace)
                {
                    switch (child.Name.LocalName)
                    {
                    case "PermissionEx":
                        this.ParsePermissionExElement(child, id.Id, "ServiceInstall");
                        break;
                    case "ServiceConfig":
                        this.ParseServiceConfigElement(child, componentId, name);
                        break;
                    case "ServiceConfigFailureActions":
                        this.ParseServiceConfigFailureActionsElement(child, componentId, name);
                        break;
                    case "ServiceDependency":
                        dependencies = String.Concat(dependencies, this.ParseServiceDependencyElement(child), "[~]");
                        break;
                    default:
                        this.Core.UnexpectedElement(node, child);
                        break;
                    }
                }
                else
                {
                    var context = new Dictionary<string, string>() { { "ServiceInstallId", id.Id }, { "ServiceInstallName", name }, { "ServiceInstallComponentId", componentId }, { "Win64", win64Component.ToString() } };
                    this.Core.ParseExtensionElement(node, child, context);
                }
            }

            if (null != dependencies)
            {
                dependencies = String.Concat(dependencies, "[~]");
            }

            if (!this.Core.EncounteredError)
            {
                var tuple = new ServiceInstallTuple(sourceLineNumbers, id)
                {
                    Name = name,
                    DisplayName = displayName,
                    ServiceType = serviceType,
                    StartType = startType,
                    ErrorControl = errorControl,
                    LoadOrderGroup = loadOrderGroup,
                    Dependencies = dependencies,
                    StartName = account,
                    Password = password,
                    Arguments = arguments,
                    ComponentRef = componentId,
                    Description  = description,
                    Interactive = interactive,
                    Vital = vital
                };

                this.Core.AddTuple(tuple);

                //var row = this.Core.CreateRow(sourceLineNumbers, TupleDefinitionType.ServiceInstall, id);
                //row.Set(1, name);
                //row.Set(2, displayName);
                //row.Set(3, typebits);
                //row.Set(4, startType);
                //row.Set(5, errorbits);
                //row.Set(6, loadOrderGroup);
                //row.Set(7, dependencies);
                //row.Set(8, account);
                //row.Set(9, password);
                //row.Set(10, arguments);
                //row.Set(11, componentId);
                //row.Set(12, description);
            }
        }

        /// <summary>
        /// Parses a SetDirectory element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        private void ParseSetDirectoryElement(XElement node)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string actionName = null;
            string id = null;
            string condition = null;
            var executionType = CustomActionExecutionType.Immediate;
            var sequences = new[] { SequenceTable.InstallUISequence, SequenceTable.InstallExecuteSequence }; // default to "both"
            string value = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Action":
                        actionName = this.Core.GetAttributeIdentifierValue(sourceLineNumbers, attrib);
                        break;
                    case "Id":
                        id = this.Core.GetAttributeIdentifierValue(sourceLineNumbers, attrib);
                        this.Core.CreateSimpleReference(sourceLineNumbers, "Directory", id);
                        break;
                    case "Sequence":
                        var sequenceValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (sequenceValue)
                        {
                        case "execute":
                            sequences = new[] { SequenceTable.InstallExecuteSequence };
                            break;
                        case "first":
                            executionType = CustomActionExecutionType.FirstSequence;
                            break;
                        case "ui":
                            sequences = new[] { SequenceTable.InstallUISequence };
                            break;
                        case "both":
                            break;
                        case "":
                            break;
                        default:
                            this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, sequenceValue, "execute", "ui", "both"));
                            break;
                        }
                        break;
                    case "Value":
                        value = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            condition = this.Core.GetConditionInnerText(node);

            if (null == id)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Id"));
            }
            else if (String.IsNullOrEmpty(actionName))
            {
                actionName = String.Concat("Set", id);
            }

            if (null == value)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Value"));
            }

            this.Core.ParseForExtensionElements(node);

            if (!this.Core.EncounteredError)
            {
                var tuple = new CustomActionTuple(sourceLineNumbers, new Identifier(AccessModifier.Public, actionName))
                {
                    ExecutionType = executionType,
                    SourceType = CustomActionSourceType.Directory,
                    TargetType = CustomActionTargetType.TextData,
                    Source = id,
                    Target = value
                };

                this.Core.AddTuple(tuple);

                foreach (var sequence in sequences)
                {
                    this.Core.ScheduleActionTuple(sourceLineNumbers, AccessModifier.Public, sequence, actionName, condition, afterAction: "CostInitialize");
                }
            }
        }

        /// <summary>
        /// Parses a SetProperty element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        private void ParseSetPropertyElement(XElement node)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string actionName = null;
            string id = null;
            string afterAction = null;
            string beforeAction = null;
            var executionType = CustomActionExecutionType.Immediate;
            var sequences = new[] { SequenceTable.InstallUISequence, SequenceTable.InstallExecuteSequence }; // default to "both"
            string value = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Action":
                        actionName = this.Core.GetAttributeIdentifierValue(sourceLineNumbers, attrib);
                        break;
                    case "Id":
                        id = this.Core.GetAttributeIdentifierValue(sourceLineNumbers, attrib);
                        break;
                    case "After":
                        afterAction = this.Core.GetAttributeIdentifierValue(sourceLineNumbers, attrib);
                        break;
                    case "Before":
                        beforeAction = this.Core.GetAttributeIdentifierValue(sourceLineNumbers, attrib);
                        break;
                    case "Sequence":
                        var sequenceValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (sequenceValue)
                        {
                        case "execute":
                            sequences = new[] { SequenceTable.InstallExecuteSequence };
                            break;
                        case "first":
                            executionType = CustomActionExecutionType.FirstSequence;
                            break;
                        case "ui":
                            sequences = new[] { SequenceTable.InstallUISequence };
                            break;
                        case "both":
                            break;
                        case "":
                            break;
                        default:
                            this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, sequenceValue, "execute", "ui", "both"));
                            break;
                        }
                        //if (0 < sequenceValue.Length)
                        //{
                        //    var sequenceType = Wix.Enums.ParseSequenceType(sequenceValue);
                        //    switch (sequenceType)
                        //    {
                        //    case Wix.SequenceType.execute:
                        //        sequences = new string[] { "InstallExecuteSequence" };
                        //        break;
                        //    case Wix.SequenceType.ui:
                        //        sequences = new string[] { "InstallUISequence" };
                        //        break;
                        //    case Wix.SequenceType.first:
                        //        firstSequence = true;
                        //        // default puts it in both sequence which is what we want
                        //        break;
                        //    case Wix.SequenceType.both:
                        //        // default so no work necessary.
                        //        break;
                        //    default:
                        //        this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, sequenceValue, "execute", "ui", "both"));
                        //        break;
                        //    }
                        //}
                        break;
                    case "Value":
                        value = this.Core.GetAttributeValue(sourceLineNumbers, attrib, EmptyRule.CanBeEmpty);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            var condition = this.Core.GetConditionInnerText(node);

            if (null == id)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Id"));
            }
            else if (String.IsNullOrEmpty(actionName))
            {
                actionName = String.Concat("Set", id);
            }

            if (null == value)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Value"));
            }

            if (null != beforeAction && null != afterAction)
            {
                this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "After", "Before"));
            }
            else if (null == beforeAction && null == afterAction)
            {
                this.Core.Write(ErrorMessages.ExpectedAttributesWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "After", "Before", "Id"));
            }

            this.Core.ParseForExtensionElements(node);

            // add the row and any references needed
            if (!this.Core.EncounteredError)
            {
                // action that is scheduled to occur before/after itself
                if (beforeAction == actionName)
                {
                    this.Core.Write(ErrorMessages.ActionScheduledRelativeToItself(sourceLineNumbers, node.Name.LocalName, "Before", beforeAction));
                }
                else if (afterAction == actionName)
                {
                    this.Core.Write(ErrorMessages.ActionScheduledRelativeToItself(sourceLineNumbers, node.Name.LocalName, "After", afterAction));
                }

                var tuple = new CustomActionTuple(sourceLineNumbers, new Identifier(AccessModifier.Public, actionName))
                {
                    ExecutionType = executionType,
                    SourceType = CustomActionSourceType.Property,
                    TargetType = CustomActionTargetType.TextData,
                    Source = id,
                    Target = value
                };

                this.Core.AddTuple(tuple);

                foreach (var sequence in sequences)
                {
                    this.Core.ScheduleActionTuple(sourceLineNumbers, AccessModifier.Public, sequence, actionName, condition, beforeAction, afterAction);
                }
            }
        }

        /// <summary>
        /// Parses a SFP catalog element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="parentSFPCatalog">Parent SFPCatalog.</param>
        private void ParseSFPFileElement(XElement node, string parentSFPCatalog)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string id = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifierValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == id)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Id"));
            }

            this.Core.ParseForExtensionElements(node);

            if (!this.Core.EncounteredError)
            {
                this.Core.AddTuple(new FileSFPCatalogTuple(sourceLineNumbers)
                {
                    FileRef = id,
                    SFPCatalogRef = parentSFPCatalog
                });
            }
        }

        /// <summary>
        /// Parses a SFP catalog element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="parentSFPCatalog">Parent SFPCatalog.</param>
        private void ParseSFPCatalogElement(XElement node, ref string parentSFPCatalog)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string parentName = null;
            string dependency = null;
            string name = null;
            string sourceFile = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Dependency":
                        dependency = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Name":
                        name = this.Core.GetAttributeShortFilename(sourceLineNumbers, attrib, false);
                        parentSFPCatalog = name;
                        break;
                    case "SourceFile":
                        sourceFile = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == name)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Name"));
            }

            if (null == sourceFile)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "SourceFile"));
            }

            foreach (var child in node.Elements())
            {
                if (CompilerCore.WixNamespace == child.Name.Namespace)
                {
                    switch (child.Name.LocalName)
                    {
                    case "SFPCatalog":
                        this.ParseSFPCatalogElement(child, ref parentName);
                        if (null != dependency && parentName == dependency)
                        {
                            this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Dependency"));
                        }
                        dependency = parentName;
                        break;
                    case "SFPFile":
                        this.ParseSFPFileElement(child, name);
                        break;
                    default:
                        this.Core.UnexpectedElement(node, child);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionElement(node, child);
                }
            }

            if (null == dependency)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Dependency"));
            }

            if (!this.Core.EncounteredError)
            {
                this.Core.AddTuple(new SFPCatalogTuple(sourceLineNumbers)
                {
                    SFPCatalog = name,
                    Catalog = sourceFile,
                    Dependency = dependency
                });
            }
        }

        /// <summary>
        /// Parses a shortcut element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="componentId">Identifer for parent component.</param>
        /// <param name="parentElementLocalName">Local name of parent element.</param>
        /// <param name="defaultTarget">Default identifier of parent (which is usually the target).</param>
        /// <param name="parentKeyPath">Flag to indicate whether the parent element is the keypath of a component or not (will only be true for file parent elements).</param>
        private void ParseShortcutElement(XElement node, string componentId, string parentElementLocalName, string defaultTarget, YesNoType parentKeyPath)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            var advertise = false;
            string arguments = null;
            string description = null;
            string descriptionResourceDll = null;
            int? descriptionResourceId = null;
            string directory = null;
            string displayResourceDll = null;
            int? displayResourceId = null;
            int? hotkey = null;
            string icon = null;
            int? iconIndex = null;
            string name = null;
            string shortName = null;
            ShortcutShowType? show = null;
            string target = null;
            string workingDirectory = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Advertise":
                        advertise = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "Arguments":
                        arguments = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Description":
                        description = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "DescriptionResourceDll":
                        descriptionResourceDll = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "DescriptionResourceId":
                        descriptionResourceId = this.Core.GetAttributeIntegerValue(sourceLineNumbers, attrib, 0, Int16.MaxValue);
                        break;
                    case "Directory":
                        directory = this.Core.CreateDirectoryReferenceFromInlineSyntax(sourceLineNumbers, attrib, null);
                        break;
                    case "DisplayResourceDll":
                        displayResourceDll = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "DisplayResourceId":
                        displayResourceId = this.Core.GetAttributeIntegerValue(sourceLineNumbers, attrib, 0, Int16.MaxValue);
                        break;
                    case "Hotkey":
                        hotkey = this.Core.GetAttributeIntegerValue(sourceLineNumbers, attrib, 0, Int16.MaxValue);
                        break;
                    case "Icon":
                        icon = this.Core.GetAttributeIdentifierValue(sourceLineNumbers, attrib);
                        this.Core.CreateSimpleReference(sourceLineNumbers, "Icon", icon);
                        break;
                    case "IconIndex":
                        iconIndex = this.Core.GetAttributeIntegerValue(sourceLineNumbers, attrib, Int16.MinValue + 1, Int16.MaxValue);
                        break;
                    case "Name":
                        name = this.Core.GetAttributeLongFilename(sourceLineNumbers, attrib, false);
                        break;
                    case "ShortName":
                        shortName = this.Core.GetAttributeShortFilename(sourceLineNumbers, attrib, false);
                        break;
                    case "Show":
                        var showValue = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        switch (showValue)
                        {
                        case "normal":
                            show = ShortcutShowType.Normal;
                            break;
                        case "maximized":
                            show = ShortcutShowType.Maximized;
                            break;
                        case "minimized":
                            show = ShortcutShowType.Minimized;
                            break;
                        case "":
                            break;
                        default:
                            this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, "Show", showValue, "normal", "maximized", "minimized"));
                            break;
                        }
                        //if (showValue.Length == 0)
                        //{
                        //    show = CompilerConstants.IllegalInteger;
                        //}
                        //else
                        //{
                        //    var showType = Wix.Shortcut.ParseShowType(showValue);
                        //    switch (showType)
                        //    {
                        //    case Wix.Shortcut.ShowType.normal:
                        //        show = 1;
                        //        break;
                        //    case Wix.Shortcut.ShowType.maximized:
                        //        show = 3;
                        //        break;
                        //    case Wix.Shortcut.ShowType.minimized:
                        //        show = 7;
                        //        break;
                        //    default:
                        //        this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, "Show", showValue, "normal", "maximized", "minimized"));
                        //        show = CompilerConstants.IllegalInteger;
                        //        break;
                        //    }
                        //}
                        break;
                    case "Target":
                        target = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "WorkingDirectory":
                        workingDirectory = this.Core.GetAttributeIdentifierValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (advertise && null != target)
            {
                this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "Target", "Advertise", "yes"));
            }

            if (null == directory)
            {
                if ("Component" == parentElementLocalName)
                {
                    directory = defaultTarget;
                }
                else
                {
                    this.Core.Write(ErrorMessages.ExpectedAttributeWhenElementNotUnderElement(sourceLineNumbers, node.Name.LocalName, "Directory", "Component"));
                }
            }

            if (null != descriptionResourceDll)
            {
                if (!descriptionResourceId.HasValue)
                {
                    this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "DescriptionResourceDll", "DescriptionResourceId"));
                }
            }
            else
            {
                if (descriptionResourceId.HasValue)
                {
                    this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "DescriptionResourceId", "DescriptionResourceDll"));
                }
            }

            if (null != displayResourceDll)
            {
                if (!displayResourceId.HasValue)
                {
                    this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "DisplayResourceDll", "DisplayResourceId"));
                }
            }
            else
            {
                if (displayResourceId.HasValue)
                {
                    this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "DisplayResourceId", "DisplayResourceDll"));
                }
            }

            if (null == name)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Name"));
            }
            else if (0 < name.Length)
            {
                if (this.Core.IsValidShortFilename(name, false))
                {
                    if (null == shortName)
                    {
                        shortName = name;
                        name = null;
                    }
                    else
                    {
                        this.Core.Write(ErrorMessages.IllegalAttributeValueWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "Name", name, "ShortName"));
                    }
                }
                else if (null == shortName) // generate a short file name.
                {
                    shortName = this.Core.CreateShortName(name, true, false, node.Name.LocalName, componentId, directory);
                }
            }

            if ("Component" != parentElementLocalName && null != target)
            {
                this.Core.Write(ErrorMessages.IllegalAttributeWhenNested(sourceLineNumbers, node.Name.LocalName, "Target", parentElementLocalName));
            }

            if (null == id)
            {
                id = this.Core.CreateIdentifier("sct", directory, LowercaseOrNull(name) ?? LowercaseOrNull(shortName));
            }

            foreach (var child in node.Elements())
            {
                if (CompilerCore.WixNamespace == child.Name.Namespace)
                {
                    switch (child.Name.LocalName)
                    {
                    case "Icon":
                        icon = this.ParseIconElement(child);
                        break;
                    case "ShortcutProperty":
                        this.ParseShortcutPropertyElement(child, id.Id);
                        break;
                    default:
                        this.Core.UnexpectedElement(node, child);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionElement(node, child);
                }
            }

            if (!this.Core.EncounteredError)
            {
                if (advertise)
                {
                    if (YesNoType.Yes != parentKeyPath && "Component" != parentElementLocalName)
                    {
                        this.Core.Write(WarningMessages.UnclearShortcut(sourceLineNumbers, id.Id, componentId, defaultTarget));
                    }

                    target = Guid.Empty.ToString("B");
                }
                else if (null != target)
                {
                }
                else if ("Component" == parentElementLocalName || "CreateFolder" == parentElementLocalName)
                {
                    target = String.Format(CultureInfo.InvariantCulture, "[{0}]", defaultTarget);
                }
                else if ("File" == parentElementLocalName)
                {
                    target = String.Format(CultureInfo.InvariantCulture, "[#{0}]", defaultTarget);
                }

                var tuple = new ShortcutTuple(sourceLineNumbers, id)
                {
                    DirectoryRef = directory,
                    Name = name,
                    ShortName = shortName,
                    ComponentRef = componentId,
                    Target = target,
                    Arguments = arguments,
                    Description = description,
                    Hotkey = hotkey,
                    IconRef = icon,
                    IconIndex = iconIndex,
                    Show = show,
                    WorkingDirectory = workingDirectory,
                    DisplayResourceDll = displayResourceDll,
                    DisplayResourceId = displayResourceId,
                    DescriptionResourceDll = descriptionResourceDll,
                    DescriptionResourceId = descriptionResourceId,
                };

                this.Core.AddTuple(tuple);
            }
        }

        /// <summary>
        /// Parses a shortcut property element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        private void ParseShortcutPropertyElement(XElement node, string shortcutId)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            string key = null;
            string value = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Key":
                        key = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Value":
                        value = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (String.IsNullOrEmpty(key))
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Key"));
            }
            else if (null == id)
            {
                id = this.Core.CreateIdentifier("scp", shortcutId, key.ToUpperInvariant());
            }

            var innerText = this.Core.GetTrimmedInnerText(node);
            if (!String.IsNullOrEmpty(innerText))
            {
                if (String.IsNullOrEmpty(value))
                {
                    value = innerText;
                }
                else // cannot specify both the value attribute and inner text
                {
                    this.Core.Write(ErrorMessages.IllegalAttributeWithInnerText(sourceLineNumbers, node.Name.LocalName, "Value"));
                }
            }

            if (String.IsNullOrEmpty(value))
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Value"));
            }

            this.Core.ParseForExtensionElements(node);

            if (!this.Core.EncounteredError)
            {
                this.Core.AddTuple(new MsiShortcutPropertyTuple(sourceLineNumbers, id)
                {
                    ShortcutRef = shortcutId,
                    PropertyKey = key,
                    PropVariantValue = value
                });
            }
        }

        /// <summary>
        /// Parses a typelib element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="componentId">Identifier of parent component.</param>
        /// <param name="fileServer">Identifier of file that acts as typelib server.</param>
        /// <param name="win64Component">true if the component is 64-bit.</param>
        private void ParseTypeLibElement(XElement node, string componentId, string fileServer, bool win64Component)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string id = null;
            var advertise = YesNoType.NotSet;
            var cost = CompilerConstants.IntegerNotSet;
            string description = null;
            var flags = 0;
            string helpDirectory = null;
            var language = CompilerConstants.IntegerNotSet;
            var majorVersion = CompilerConstants.IntegerNotSet;
            var minorVersion = CompilerConstants.IntegerNotSet;
            var resourceId = CompilerConstants.LongNotSet;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeGuidValue(sourceLineNumbers, attrib, false);
                        break;
                    case "Advertise":
                        advertise = this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "Control":
                        if (YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib))
                        {
                            flags |= 2;
                        }
                        break;
                    case "Cost":
                        cost = this.Core.GetAttributeIntegerValue(sourceLineNumbers, attrib, 0, Int32.MaxValue);
                        break;
                    case "Description":
                        description = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "HasDiskImage":
                        if (YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib))
                        {
                            flags |= 8;
                        }
                        break;
                    case "HelpDirectory":
                        helpDirectory = this.Core.CreateDirectoryReferenceFromInlineSyntax(sourceLineNumbers, attrib, null);
                        break;
                    case "Hidden":
                        if (YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib))
                        {
                            flags |= 4;
                        }
                        break;
                    case "Language":
                        language = this.Core.GetAttributeIntegerValue(sourceLineNumbers, attrib, 0, Int16.MaxValue);
                        break;
                    case "MajorVersion":
                        majorVersion = this.Core.GetAttributeIntegerValue(sourceLineNumbers, attrib, 0, UInt16.MaxValue);
                        break;
                    case "MinorVersion":
                        minorVersion = this.Core.GetAttributeIntegerValue(sourceLineNumbers, attrib, 0, Byte.MaxValue);
                        break;
                    case "ResourceId":
                        resourceId = this.Core.GetAttributeLongValue(sourceLineNumbers, attrib, Int32.MinValue, Int32.MaxValue);
                        break;
                    case "Restricted":
                        if (YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib))
                        {
                            flags |= 1;
                        }
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == id)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Id"));
            }

            if (CompilerConstants.IntegerNotSet == language)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Language"));
                language = CompilerConstants.IllegalInteger;
            }

            // build up the typelib version string for the registry if the major or minor version was specified
            string registryVersion = null;
            if (CompilerConstants.IntegerNotSet != majorVersion || CompilerConstants.IntegerNotSet != minorVersion)
            {
                if (CompilerConstants.IntegerNotSet != majorVersion)
                {
                    registryVersion = majorVersion.ToString("x", CultureInfo.InvariantCulture.NumberFormat);
                }
                else
                {
                    registryVersion = "0";
                }

                if (CompilerConstants.IntegerNotSet != minorVersion)
                {
                    registryVersion = String.Concat(registryVersion, ".", minorVersion.ToString("x", CultureInfo.InvariantCulture.NumberFormat));
                }
                else
                {
                    registryVersion = String.Concat(registryVersion, ".0");
                }
            }

            // if the advertise state has not been set, default to non-advertised
            if (YesNoType.NotSet == advertise)
            {
                advertise = YesNoType.No;
            }

            foreach (var child in node.Elements())
            {
                if (CompilerCore.WixNamespace == child.Name.Namespace)
                {
                    switch (child.Name.LocalName)
                    {
                    case "AppId":
                        this.ParseAppIdElement(child, componentId, YesNoType.NotSet, fileServer, id, registryVersion);
                        break;
                    case "Class":
                        this.ParseClassElement(child, componentId, YesNoType.NotSet, fileServer, id, registryVersion, null);
                        break;
                    case "Interface":
                        this.ParseInterfaceElement(child, componentId, null, null, id, registryVersion);
                        break;
                    default:
                        this.Core.UnexpectedElement(node, child);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionElement(node, child);
                }
            }


            if (YesNoType.Yes == advertise)
            {
                if (CompilerConstants.LongNotSet != resourceId)
                {
                    this.Core.Write(ErrorMessages.IllegalAttributeWhenAdvertised(sourceLineNumbers, node.Name.LocalName, "ResourceId"));
                }

                if (0 != flags)
                {
                    if (0x1 == (flags & 0x1))
                    {
                        this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "Restricted", "Advertise", "yes"));
                    }

                    if (0x2 == (flags & 0x2))
                    {
                        this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "Control", "Advertise", "yes"));
                    }

                    if (0x4 == (flags & 0x4))
                    {
                        this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "Hidden", "Advertise", "yes"));
                    }

                    if (0x8 == (flags & 0x8))
                    {
                        this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "HasDiskImage", "Advertise", "yes"));
                    }
                }

                if (!this.Core.EncounteredError)
                {
                    var tuple = new TypeLibTuple(sourceLineNumbers)
                    {
                        LibId = id,
                        Language = language,
                        ComponentRef = componentId,
                        Description = description,
                        DirectoryRef = helpDirectory,
                        FeatureRef = Guid.Empty.ToString("B")
                    };

                    if (CompilerConstants.IntegerNotSet != majorVersion || CompilerConstants.IntegerNotSet != minorVersion)
                    {
                        tuple.Version = (CompilerConstants.IntegerNotSet != majorVersion ? majorVersion * 256 : 0) + (CompilerConstants.IntegerNotSet != minorVersion ? minorVersion : 0);
                    }

                    if (CompilerConstants.IntegerNotSet != cost)
                    {
                        tuple.Cost = cost;
                    }

                    this.Core.AddTuple(tuple);
                }
            }
            else if (YesNoType.No == advertise)
            {
                if (CompilerConstants.IntegerNotSet != cost && CompilerConstants.IllegalInteger != cost)
                {
                    this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "Cost", "Advertise", "no"));
                }

                if (null == fileServer)
                {
                    this.Core.Write(ErrorMessages.MissingTypeLibFile(sourceLineNumbers, node.Name.LocalName, "File"));
                }

                if (null == registryVersion)
                {
                    this.Core.Write(ErrorMessages.ExpectedAttributesWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "MajorVersion", "MinorVersion", "Advertise", "no"));
                }

                // HKCR\TypeLib\[ID]\[MajorVersion].[MinorVersion], (Default) = [Description]
                this.Core.CreateRegistryRow(sourceLineNumbers, RegistryRootType.ClassesRoot, String.Format(CultureInfo.InvariantCulture, @"TypeLib\{0}\{1}", id, registryVersion), null, description, componentId);

                // HKCR\TypeLib\[ID]\[MajorVersion].[MinorVersion]\[Language]\[win16|win32|win64], (Default) = [TypeLibPath]\[ResourceId]
                var path = String.Concat("[#", fileServer, "]");
                if (CompilerConstants.LongNotSet != resourceId)
                {
                    path = String.Concat(path, Path.DirectorySeparatorChar, resourceId.ToString(CultureInfo.InvariantCulture.NumberFormat));
                }
                this.Core.CreateRegistryRow(sourceLineNumbers, RegistryRootType.ClassesRoot, String.Format(CultureInfo.InvariantCulture, @"TypeLib\{0}\{1}\{2}\{3}", id, registryVersion, language, (win64Component ? "win64" : "win32")), null, path, componentId);

                // HKCR\TypeLib\[ID]\[MajorVersion].[MinorVersion]\FLAGS, (Default) = [TypeLibFlags]
                this.Core.CreateRegistryRow(sourceLineNumbers, RegistryRootType.ClassesRoot, String.Format(CultureInfo.InvariantCulture, @"TypeLib\{0}\{1}\FLAGS", id, registryVersion), null, flags.ToString(CultureInfo.InvariantCulture.NumberFormat), componentId);

                if (null != helpDirectory)
                {
                    // HKCR\TypeLib\[ID]\[MajorVersion].[MinorVersion]\HELPDIR, (Default) = [HelpDirectory]
                    this.Core.CreateRegistryRow(sourceLineNumbers, RegistryRootType.ClassesRoot, String.Format(CultureInfo.InvariantCulture, @"TypeLib\{0}\{1}\HELPDIR", id, registryVersion), null, String.Concat("[", helpDirectory, "]"), componentId);
                }
            }
        }

        /// <summary>
        /// Parses an upgrade element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        private void ParseUpgradeElement(XElement node)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string id = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeGuidValue(sourceLineNumbers, attrib, false);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == id)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Id"));
            }

            // process the UpgradeVersion children here
            foreach (var child in node.Elements())
            {
                if (CompilerCore.WixNamespace == child.Name.Namespace)
                {
                    var childSourceLineNumbers = Preprocessor.GetSourceLineNumbers(child);

                    switch (child.Name.LocalName)
                    {
                    case "Property":
                        this.ParsePropertyElement(child);
                        this.Core.Write(WarningMessages.DeprecatedUpgradeProperty(childSourceLineNumbers));
                        break;
                    case "UpgradeVersion":
                        this.ParseUpgradeVersionElement(child, id);
                        break;
                    default:
                        this.Core.UnexpectedElement(node, child);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionElement(node, child);
                }
            }

            // No rows created here. All row creation is done in ParseUpgradeVersionElement.
        }

        /// <summary>
        /// Parse upgrade version element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="upgradeId">Upgrade code.</param>
        private void ParseUpgradeVersionElement(XElement node, string upgradeId)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);

            string actionProperty = null;
            string language = null;
            string maximum = null;
            string minimum = null;
            var excludeLanguages = false;
            var ignoreFailures = false;
            var includeMax = false;
            var includeMin = true;
            var migrateFeatures = false;
            var onlyDetect = false;
            string removeFeatures = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "ExcludeLanguages":
                        excludeLanguages = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "IgnoreRemoveFailure":
                        ignoreFailures = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "IncludeMaximum":
                        includeMax = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "IncludeMinimum": // this is "yes" by default
                        includeMin = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "Language":
                        language = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Minimum":
                        minimum = this.Core.GetAttributeVersionValue(sourceLineNumbers, attrib);
                        break;
                    case "Maximum":
                        maximum = this.Core.GetAttributeVersionValue(sourceLineNumbers, attrib);
                        break;
                    case "MigrateFeatures":
                        migrateFeatures = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "OnlyDetect":
                        onlyDetect = YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                        break;
                    case "Property":
                        actionProperty = this.Core.GetAttributeIdentifierValue(sourceLineNumbers, attrib);
                        break;
                    case "RemoveFeatures":
                        removeFeatures = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == actionProperty)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Property"));
            }
            else if (actionProperty.ToUpper(CultureInfo.InvariantCulture) != actionProperty)
            {
                this.Core.Write(ErrorMessages.SecurePropertyNotUppercase(sourceLineNumbers, node.Name.LocalName, "Property", actionProperty));
            }

            if (null == minimum && null == maximum)
            {
                this.Core.Write(ErrorMessages.ExpectedAttributes(sourceLineNumbers, node.Name.LocalName, "Minimum", "Maximum"));
            }

            this.Core.ParseForExtensionElements(node);

            if (!this.Core.EncounteredError)
            {
                var tuple = new UpgradeTuple(sourceLineNumbers)
                {
                    UpgradeCode = upgradeId,
                    VersionMin = minimum,
                    VersionMax = maximum,
                    Language = language,
                    ExcludeLanguages = excludeLanguages,
                    IgnoreRemoveFailures = ignoreFailures,
                    VersionMaxInclusive = includeMax,
                    VersionMinInclusive = includeMin,
                    MigrateFeatures = migrateFeatures,
                    OnlyDetect = onlyDetect,
                    Remove = removeFeatures,
                    ActionProperty = actionProperty
                };

                this.Core.AddTuple(tuple);

                // Ensure the action property is secure.
                this.AddWixPropertyRow(sourceLineNumbers, new Identifier(AccessModifier.Private, actionProperty), false, true, false);

                // Ensure that RemoveExistingProducts is authored in InstallExecuteSequence
                // if at least one row in Upgrade table lacks the OnlyDetect attribute.
                if (onlyDetect)
                {
                    this.Core.CreateSimpleReference(sourceLineNumbers, "WixAction", "InstallExecuteSequence", "RemoveExistingProducts");
                }
            }
        }

        /// <summary>
        /// Parses a verb element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        /// <param name="extension">Extension verb is releated to.</param>
        /// <param name="progId">Optional progId for extension.</param>
        /// <param name="componentId">Identifier for parent component.</param>
        /// <param name="advertise">Flag if verb is advertised.</param>
        private void ParseVerbElement(XElement node, string extension, string progId, string componentId, YesNoType advertise)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string id = null;
            string argument = null;
            string command = null;
            var sequence = CompilerConstants.IntegerNotSet;
            string target = null;
            string targetFile = null;
            string targetProperty = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Argument":
                        argument = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Command":
                        command = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    case "Sequence":
                        sequence = this.Core.GetAttributeIntegerValue(sourceLineNumbers, attrib, 1, Int16.MaxValue);
                        break;
                    case "Target":
                        target = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        this.Core.Write(WarningMessages.DeprecatedAttribute(sourceLineNumbers, node.Name.LocalName, attrib.Name.LocalName, "TargetFile", "TargetProperty"));
                        break;
                    case "TargetFile":
                        targetFile = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        this.Core.CreateSimpleReference(sourceLineNumbers, "File", targetFile);
                        break;
                    case "TargetProperty":
                        targetProperty = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == id)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Id"));
            }

            if (null != target && null != targetFile)
            {
                this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "Target", "TargetFile"));
            }

            if (null != target && null != targetProperty)
            {
                this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "Target", "TargetProperty"));
            }

            if (null != targetFile && null != targetProperty)
            {
                this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "TargetFile", "TargetProperty"));
            }

            this.Core.ParseForExtensionElements(node);

            if (YesNoType.Yes == advertise)
            {
                if (null != target)
                {
                    this.Core.Write(ErrorMessages.IllegalAttributeWhenAdvertised(sourceLineNumbers, node.Name.LocalName, "Target"));
                }

                if (null != targetFile)
                {
                    this.Core.Write(ErrorMessages.IllegalAttributeWhenAdvertised(sourceLineNumbers, node.Name.LocalName, "TargetFile"));
                }

                if (null != targetProperty)
                {
                    this.Core.Write(ErrorMessages.IllegalAttributeWhenAdvertised(sourceLineNumbers, node.Name.LocalName, "TargetProperty"));
                }

                if (!this.Core.EncounteredError)
                {
                    var tuple = new VerbTuple(sourceLineNumbers)
                    {
                        ExtensionRef = extension,
                        Verb = id,
                        Command = command,
                        Argument = argument,
                    };

                    if (CompilerConstants.IntegerNotSet != sequence)
                    {
                        tuple.Sequence = sequence;
                    }

                    this.Core.AddTuple(tuple);
                }
            }
            else if (YesNoType.No == advertise)
            {
                if (CompilerConstants.IntegerNotSet != sequence)
                {
                    this.Core.Write(ErrorMessages.IllegalAttributeWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "Sequence", "Advertise", "no"));
                }

                if (null == target && null == targetFile && null == targetProperty)
                {
                    this.Core.Write(ErrorMessages.ExpectedAttributesWithOtherAttribute(sourceLineNumbers, node.Name.LocalName, "TargetFile", "TargetProperty", "Advertise", "no"));
                }

                if (null == target)
                {
                    if (null != targetFile)
                    {
                        target = String.Concat("\"[#", targetFile, "]\"");
                    }

                    if (null != targetProperty)
                    {
                        target = String.Concat("\"[", targetProperty, "]\"");
                    }
                }

                if (null != argument)
                {
                    target = String.Concat(target, " ", argument);
                }

                var prefix = (null != progId ? progId : String.Concat(".", extension));

                if (null != command)
                {
                    this.Core.CreateRegistryRow(sourceLineNumbers, RegistryRootType.ClassesRoot, String.Concat(prefix, "\\shell\\", id), String.Empty, command, componentId);
                }

                this.Core.CreateRegistryRow(sourceLineNumbers, RegistryRootType.ClassesRoot, String.Concat(prefix, "\\shell\\", id, "\\command"), String.Empty, target, componentId);
            }
        }

        /// <summary>
        /// Parses a WixVariable element.
        /// </summary>
        /// <param name="node">Element to parse.</param>
        private void ParseWixVariableElement(XElement node)
        {
            var sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            Identifier id = null;
            var overridable = false;
            string value = null;

            foreach (var attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || CompilerCore.WixNamespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                    case "Id":
                        id = this.Core.GetAttributeIdentifier(sourceLineNumbers, attrib);
                        break;
                    case "Overridable":
                        overridable = (YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib));
                        break;
                    case "Value":
                        value = this.Core.GetAttributeValue(sourceLineNumbers, attrib, EmptyRule.CanBeEmpty);
                        break;
                    default:
                        this.Core.UnexpectedAttribute(node, attrib);
                        break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            if (null == id)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Id"));
            }

            if (null == value)
            {
                this.Core.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Value"));
            }

            this.Core.ParseForExtensionElements(node);

            if (!this.Core.EncounteredError)
            {
                this.Core.AddTuple(new WixVariableTuple(sourceLineNumbers, id)
                {
                    Value = value,
                    Overridable = overridable
                });
            }
        }

        private CompressionLevel? ParseCompressionLevel(SourceLineNumber sourceLineNumbers, XElement node, XAttribute attribute)
        {
            var compressionLevel = this.Core.GetAttributeValue(sourceLineNumbers, attribute);
            switch (compressionLevel)
            {
            case "high":
                return CompressionLevel.High;
            case "low":
                return CompressionLevel.Low;
            case "medium":
                return CompressionLevel.Medium;
            case "mszip":
                return CompressionLevel.Mszip;
            case "none":
                return CompressionLevel.None;
            case "":
                break;
            default:
                this.Core.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, attribute.Name.LocalName, compressionLevel, "high", "low", "medium", "mszip", "none"));
                break;
            }

            return null;
        }
    }
}
