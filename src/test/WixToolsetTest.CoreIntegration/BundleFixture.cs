// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolsetTest.CoreIntegration
{
    using System.IO;
    using System.Linq;
    using WixBuildTools.TestSupport;
    using WixToolset.Core.TestPackage;
    using WixToolset.Data;
    using WixToolset.Data.Tuples;
    using Xunit;

    public class BundleFixture
    {
        [Fact]
        public void CanBuildSimpleBundle()
        {
            var burnStubPath = TestData.Get(@"TestData\.Data\burn.exe");
            var folder = TestData.Get(@"TestData\SimpleBundle");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "Bundle.wxs"),
                    "-loc", Path.Combine(folder, "Bundle.en-us.wxl"),
                    "-bindpath", Path.Combine(folder, "data"),
                    "-intermediateFolder", intermediateFolder,
                    "-burnStub", burnStubPath,
                    "-o", Path.Combine(baseFolder, @"bin\test.exe")
                });

                result.AssertSuccess();

                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.exe")));
#if TODO
                Assert.True(File.Exists(Path.Combine(baseFolder, @"bin\test.wixpdb")));
#endif

                var intermediate = Intermediate.Load(Path.Combine(intermediateFolder, @"test.wir"));
                var section = intermediate.Sections.Single();

                var msiTuple = section.Tuples.OfType<WixBundlePackageTuple>().Single();
                Assert.Equal("test.msi", msiTuple.Id.Id );
            }
        }
    }
}