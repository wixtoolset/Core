// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.WindowsInstaller.Unbind
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using WixToolset.Core.Native;
    using WixToolset.Data;
    using WixToolset.Extensibility;
    using WixToolset.Extensibility.Data;
    using WixToolset.Extensibility.Services;
    using WixToolset.Msi;

    internal class DecompileMsiOrMsmCommand
    {
        public DecompileMsiOrMsmCommand(IDecompileContext context, IEnumerable<IWindowsInstallerBackendDecompilerExtension> backendExtensions)
        {
            this.Context = context;
            this.Extensions = backendExtensions;
            this.Messaging = context.ServiceProvider.GetService<IMessaging>();
        }

        private IDecompileContext Context { get; }

        private IEnumerable<IWindowsInstallerBackendDecompilerExtension> Extensions { get; }

        private IMessaging Messaging { get; }

        public DecompileResult Execute()
        {
            var result = new DecompileResult();

            try
            {
                using (var database = new Database(this.Context.DecompilePath, OpenDatabase.ReadOnly))
                {
                    var unbindCommand = new UnbindDatabaseCommand(this.Messaging, database, this.Context.DecompilePath, this.Context.DecompileType, this.Context.ExtractFolder, this.Context.IntermediateFolder, this.Context.IsAdminImage, false, skipSummaryInfo: false);
                    var output = unbindCommand.Execute();
                    var extractedFilePaths = new List<string>(unbindCommand.ExportedFiles);

                    var decompiler = new Decompiler(this.Messaging, this.Extensions, this.Context.BaseSourcePath, this.Context.SuppressCustomTables, this.Context.SuppressDroppingEmptyTables, this.Context.SuppressUI, this.Context.TreatProductAsModule);
                    result.Document = decompiler.Decompile(output);

                    // extract the files from the cabinets
                    if (!String.IsNullOrEmpty(this.Context.ExtractFolder) && !this.Context.SuppressExtractCabinets)
                    {
                        var fileDirectory = Path.Combine(this.Context.ExtractFolder, "File");

                        var extractCommand = new ExtractCabinetsCommand(output, database, this.Context.DecompilePath, fileDirectory, this.Context.IntermediateFolder, this.Context.TreatProductAsModule);
                        extractCommand.Execute();

                        extractedFilePaths.AddRange(extractCommand.ExtractedFiles);
                        result.ExtractedFilePaths = extractedFilePaths;
                    }
                    else
                    {
                        result.ExtractedFilePaths = new string[0];
                    }
                }
            }
            catch (Win32Exception e)
            {
                if (0x6E == e.NativeErrorCode) // ERROR_OPEN_FAILED
                {
                    throw new WixException(ErrorMessages.OpenDatabaseFailed(this.Context.DecompilePath));
                }

                throw;
            }

            return result;
        }
    }
}
