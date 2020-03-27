// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolsetTest.CoreIntegration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Example.Extension;
    using WixBuildTools.TestSupport;
    using WixToolset.Core.TestPackage;
    using Xunit;

    public class BundleManifestFixture
    {
        [Fact]
        public void PopulatesManifestWithBundleExtension()
        {
            var burnStubPath = TestData.Get(@"TestData\.Data\burn.exe");
            var folder = TestData.Get(@"TestData");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");
                var bundlePath = Path.Combine(baseFolder, @"bin\test.exe");
                var baFolderPath = Path.Combine(baseFolder, "ba");
                var extractFolderPath = Path.Combine(baseFolder, "extract");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "BundleExtension", "BundleExtension.wxs"),
                    Path.Combine(folder, "BundleExtension", "SimpleBundleExtension.wxs"),
                    Path.Combine(folder, "BundleWithPackageGroupRef", "MinimalPackageGroup.wxs"),
                    Path.Combine(folder, "BundleWithPackageGroupRef", "Bundle.wxs"),
                    "-bindpath", Path.Combine(folder, "SimpleBundle", "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-burnStub", burnStubPath,
                    "-o", bundlePath
                });

                result.AssertSuccess();

                Assert.True(File.Exists(bundlePath));

                var extractResult = BundleExtractor.ExtractBAContainer(null, bundlePath, baFolderPath, extractFolderPath);
                extractResult.AssertSuccess();

                var bundleExtensions = extractResult.SelectManifestNodes("/burn:BurnManifest/burn:BundleExtension");
                Assert.Equal(1, bundleExtensions.Count);
                Assert.Equal("<BundleExtension Id='ExampleBext' EntryPayloadId='ExampleBext' />", bundleExtensions[0].GetTestXml());

                var bundleExtensionPayloads = extractResult.SelectManifestNodes("/burn:BurnManifest/burn:UX/burn:Payload[@Id='ExampleBext']");
                Assert.Equal(1, bundleExtensionPayloads.Count);
                var ignored = new Dictionary<string, List<string>>
                {
                    { "Payload", new List<string> { "FileSize", "Hash", "SourcePath" } },
                };
                Assert.Equal("<Payload Id='ExampleBext' FilePath='fakebext.dll' FileSize='*' Hash='*' Packaging='embedded' SourcePath='*' />", bundleExtensionPayloads[0].GetTestXml(ignored));
            }
        }

        [Fact]
        public void PopulatesManifestWithBundleExtensionSearches()
        {
            var burnStubPath = TestData.Get(@"TestData\.Data\burn.exe");
            var extensionPath = Path.GetFullPath(new Uri(typeof(ExampleExtensionFactory).Assembly.CodeBase).LocalPath);
            var folder = TestData.Get(@"TestData");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");
                var bundlePath = Path.Combine(baseFolder, @"bin\test.exe");
                var baFolderPath = Path.Combine(baseFolder, "ba");
                var extractFolderPath = Path.Combine(baseFolder, "extract");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "BundleExtension", "BundleExtensionSearches.wxs"),
                    Path.Combine(folder, "BundleExtension", "BundleWithSearches.wxs"),
                    Path.Combine(folder, "BundleWithPackageGroupRef", "MinimalPackageGroup.wxs"),
                    Path.Combine(folder, "BundleWithPackageGroupRef", "Bundle.wxs"),
                    "-ext", extensionPath,
                    "-bindpath", Path.Combine(folder, "SimpleBundle", "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-burnStub", burnStubPath,
                    "-o", bundlePath
                });

                result.AssertSuccess();

                Assert.True(File.Exists(bundlePath));

                var extractResult = BundleExtractor.ExtractBAContainer(null, bundlePath, baFolderPath, extractFolderPath);
                extractResult.AssertSuccess();

                var bundleExtensions = extractResult.SelectManifestNodes("/burn:BurnManifest/burn:BundleExtension");
                Assert.Equal(1, bundleExtensions.Count);
                Assert.Equal("<BundleExtension Id='ExampleBundleExtension' EntryPayloadId='ExampleBundleExtension' />", bundleExtensions[0].GetTestXml());

                var extensionSearches = extractResult.SelectManifestNodes("/burn:BurnManifest/burn:ExtensionSearch");
                Assert.Equal(2, extensionSearches.Count);
                Assert.Equal("<ExtensionSearch Id='ExampleSearchBar' Variable='SearchBar' Condition='WixBundleInstalled' ExtensionId='ExampleBundleExtension' />", extensionSearches[0].GetTestXml());
                Assert.Equal("<ExtensionSearch Id='ExampleSearchFoo' Variable='SearchFoo' ExtensionId='ExampleBundleExtension' />", extensionSearches[1].GetTestXml());

                var bundleExtensionDatas = extractResult.SelectBundleExtensionDataNodes("/be:BundleExtensionData/be:BundleExtension[@Id='ExampleBundleExtension']");
                Assert.Equal(1, bundleExtensionDatas.Count);
                Assert.Equal("<BundleExtension Id='ExampleBundleExtension'>" +
                    "<ExampleSearch Id='ExampleSearchBar' SearchFor='Bar' />" +
                    "<ExampleSearch Id='ExampleSearchFoo' SearchFor='Foo' />" +
                    "</BundleExtension>", bundleExtensionDatas[0].GetTestXml());
            }
        }
    }
}
