// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolsetTest.CoreIntegration
{
    using System;
    using System.IO;
    using WixBuildTools.TestSupport;
    using WixToolset.Core.TestPackage;
    using Xunit;

    public class BadInputFixture
    {
        [Fact(Skip = "Test demonstrates failure")]
        public void RegistryKeyWithoutAttributesDoesntCrash()
        {
            var folder = TestData.Get(@"TestData\BadInput");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var intermediateFolder = Path.Combine(baseFolder, "obj");
                var wixlibPath = Path.Combine(intermediateFolder, @"test.wixlib");

                var result = WixRunner.Execute(new[]
                {
                    "build",
                    Path.Combine(folder, "RegistryKey.wxs"),
                    "-intermediateFolder", intermediateFolder,
                    "-o", wixlibPath,
                });

                Assert.InRange(result.ExitCode, 2, Int32.MaxValue);
            }
        }
    }
}
