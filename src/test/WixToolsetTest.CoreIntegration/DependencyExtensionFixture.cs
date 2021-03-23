// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolsetTest.CoreIntegration
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using WixBuildTools.TestSupport;
    using WixToolset.Core.TestPackage;
    using Xunit;

    public class DependencyExtensionFixture
    {
        [Fact]
        public void CanBuildBundleUsingExePackageWithProvides()
        {
            var folder = TestData.Get(@"TestData");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");
                var binFolder = Path.Combine(baseFolder, "bin");
                var bundlePath = Path.Combine(binFolder, "test.exe");
                var baFolderPath = Path.Combine(baseFolder, "ba");
                var extractFolderPath = Path.Combine(baseFolder, "extract");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Dependency", "ExePackageProvidesBundle.wxs"),
                    Path.Combine(folder, "BundleWithPackageGroupRef", "Bundle.wxs"),
                    "-bindpath", Path.Combine(folder, ".Data"),
                    "-bindpath", Path.Combine(folder, "SimpleBundle", "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", bundlePath,
                });

                result.AssertSuccess();

                Assert.True(File.Exists(bundlePath));

                var extractResult = BundleExtractor.ExtractBAContainer(null, bundlePath, baFolderPath, extractFolderPath);
                extractResult.AssertSuccess();

                var provides = extractResult.SelectManifestNodes("/burn:BurnManifest/burn:Chain/burn:ExePackage/burn:Provides")
                                            .Cast<XmlElement>()
                                            .Select(e => e.GetTestXml())
                                            .ToArray();
                WixAssert.CompareLineByLine(new string[]
                {
                    "<Provides Key='DependencyTests_ExeA,v1.0' Version='1.0.0.0' DisplayName='Windows Installer XML Toolset' />",
                }, provides);
            }
        }

        [Fact]
        public void CanBuildBundleUsingMsiWithProvides()
        {
            var folder = TestData.Get(@"TestData");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");
                var binFolder = Path.Combine(baseFolder, "bin");
                var bundlePath = Path.Combine(binFolder, "test.exe");
                var baFolderPath = Path.Combine(baseFolder, "ba");
                var extractFolderPath = Path.Combine(baseFolder, "extract");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "UsingProvides", "Package.wxs"),
                    Path.Combine(folder, "UsingProvides", "PackageComponents.wxs"),
                    "-loc", Path.Combine(folder, "UsingProvides", "Package.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "UsingProvides"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", Path.Combine(binFolder, "UsingProvides.msi"),
                });

                result.AssertSuccess();

                result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Dependency", "UsingProvidesBundle.wxs"),
                    Path.Combine(folder, "BundleWithPackageGroupRef", "Bundle.wxs"),
                    "-bindpath", Path.Combine(folder, "SimpleBundle", "data"),
                    "-bindpath", binFolder,
                    "-intermediateFolder", intermediateFolder,
                    "-o", bundlePath,
                });

                result.AssertSuccess();

                Assert.True(File.Exists(bundlePath));

                var extractResult = BundleExtractor.ExtractBAContainer(null, bundlePath, baFolderPath, extractFolderPath);
                extractResult.AssertSuccess();

                var provides = extractResult.SelectManifestNodes("/burn:BurnManifest/burn:Chain/burn:MsiPackage/burn:Provides")
                                            .Cast<XmlElement>()
                                            .Select(e => e.GetTestXml())
                                            .ToArray();
                WixAssert.CompareLineByLine(new string[]
                {
                    "<Provides Key='UsingProvides' Version='1.0.0.0' DisplayName='MsiPackage' Imported='yes' />",
                    "<Provides Key='{A81D50F9-B696-4F3D-ABE0-E64D61590E5F}' Version='1.0.0.0' DisplayName='MsiPackage' />",
                }, provides);
            }
        }

        [Fact]
        public void CanBuildBundleWithCustomProviderKey()
        {
            var folder = TestData.Get(@"TestData");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");
                var binFolder = Path.Combine(baseFolder, "bin");
                var bundlePath = Path.Combine(binFolder, "test.exe");
                var baFolderPath = Path.Combine(baseFolder, "ba");
                var extractFolderPath = Path.Combine(baseFolder, "extract");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Dependency", "CustomProviderKeyBundle.wxs"),
                    Path.Combine(folder, "BundleWithPackageGroupRef", "MinimalPackageGroup.wxs"),
                    "-bindpath", Path.Combine(folder, "SimpleBundle", "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", bundlePath,
                });

                result.AssertSuccess();

                Assert.True(File.Exists(bundlePath));

                var extractResult = BundleExtractor.ExtractBAContainer(null, bundlePath, baFolderPath, extractFolderPath);
                extractResult.AssertSuccess();

                var ignoreAttributesByElementName = new Dictionary<string, List<string>>
                {
                    { "Registration", new List<string> { "Id" } },
                };
                var registration = extractResult.SelectManifestNodes("/burn:BurnManifest/burn:Registration")
                                                .Cast<XmlElement>()
                                                .Select(e => e.GetTestXml(ignoreAttributesByElementName))
                                                .ToArray();
                WixAssert.CompareLineByLine(new string[]
                {
                    "<Registration Id='*' ExecutableName='test.exe' PerMachine='yes' Tag='' Version='1.0.0.0' ProviderKey='MyProviderKey,v1.0'><Arp Register='yes' DisplayName='BurnBundle' DisplayVersion='1.0.0.0' Publisher='Example Corporation' /></Registration>",
                }, registration);
            }
        }

        [Fact]
        public void CanBuildPackageUsingProvides()
        {
            var folder = TestData.Get(@"TestData\UsingProvides");
            var build = new Builder(folder, null, new[] { folder });

            var results = build.BuildAndQuery(Build, "WixDependencyProvider");
            Assert.Equal(new[]
            {
                "WixDependencyProvider:dep74OfIcniaqxA7EprRGBw4Oyy3r8\tfilF5_pLhBuF5b4N9XEo52g_hUM5Lo\tUsingProvides\t\t\t",
            }, results);
        }

        private static void Build(string[] args)
        {
            var result = WixRunner.Execute(args)
                                  .AssertSuccess();
        }
    }
}
