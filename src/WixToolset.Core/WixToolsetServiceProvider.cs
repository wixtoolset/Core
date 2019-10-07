// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core
{
    using System;
    using System.Collections.Generic;
    using WixToolset.Core.CommandLine;
    using WixToolset.Core.ExtensibilityServices;
    using WixToolset.Data;
    using WixToolset.Extensibility.Data;
    using WixToolset.Extensibility.Services;

    public sealed class WixToolsetServiceProvider : IServiceProvider
    {
        public WixToolsetServiceProvider()
        {
            this.CreationFunctions = new Dictionary<Type, Func<IServiceProvider, Dictionary<Type, object>, object>>();
            this.Singletons = new Dictionary<Type, object>();

            // Singletons.
            this.AddService((provider, singletons) => AddSingleton<IExtensionManager>(singletons, new ExtensionManager(provider)));
            this.AddService((provider, singletons) => AddSingleton<IMessaging>(singletons, new Messaging()));
            this.AddService((provider, singletons) => AddSingleton<ITupleDefinitionCreator>(singletons, new TupleDefinitionCreator(provider)));
            this.AddService((provider, singletons) => AddSingleton<IParseHelper>(singletons, new ParseHelper(provider)));
            this.AddService((provider, singletons) => AddSingleton<IPreprocessHelper>(singletons, new PreprocessHelper(provider)));
            this.AddService((provider, singletons) => AddSingleton<IBackendHelper>(singletons, new BackendHelper(provider)));
            this.AddService((provider, singletons) => AddSingleton<IPathResolver>(singletons, new PathResolver()));
            this.AddService((provider, singletons) => AddSingleton<IWindowsInstallerBackendHelper>(singletons, new WindowsInstallerBackendHelper()));

            // Transients.
            this.AddService<ICommandLineArguments>((provider, singletons) => new CommandLineArguments(provider));
            this.AddService<ICommandLineContext>((provider, singletons) => new CommandLineContext(provider));
            this.AddService<ICommandLine>((provider, singletons) => new CommandLine.CommandLine(provider));
            this.AddService<IPreprocessContext>((provider, singletons) => new PreprocessContext(provider));
            this.AddService<ICompileContext>((provider, singletons) => new CompileContext(provider));
            this.AddService<ILibraryContext>((provider, singletons) => new LibraryContext(provider));
            this.AddService<ILinkContext>((provider, singletons) => new LinkContext(provider));
            this.AddService<IResolveContext>((provider, singletons) => new ResolveContext(provider));
            this.AddService<IBindContext>((provider, singletons) => new BindContext(provider));
            this.AddService<IDecompileContext>((provider, singletons) => new DecompileContext(provider));
            this.AddService<ILayoutContext>((provider, singletons) => new LayoutContext(provider));
            this.AddService<IInscribeContext>((provider, singletons) => new InscribeContext(provider));

            this.AddService<IBindFileWithPath>((provider, singletons) => new BindFileWithPath());
            this.AddService<IBindPath>((provider, singletons) => new BindPath());
            this.AddService<IBindResult>((provider, singletons) => new BindResult());
            this.AddService<IComponentKeyPath>((provider, singletons) => new ComponentKeyPath());
            this.AddService<IDecompileResult>((provider, singletons) => new DecompileResult());
            this.AddService<IIncludedFile>((provider, singletons) => new IncludedFile());
            this.AddService<IPreprocessResult>((provider, singletons) => new PreprocessResult());
            this.AddService<IResolvedDirectory>((provider, singletons) => new ResolvedDirectory());
            this.AddService<IResolveFileResult>((provider, singletons) => new ResolveFileResult());
            this.AddService<IResolveResult>((provider, singletons) => new ResolveResult());
            this.AddService<IResolvedCabinet>((provider, singletons) => new ResolvedCabinet());
            this.AddService<IVariableResolution>((provider, singletons) => new VariableResolution());

            this.AddService<IBinder>((provider, singletons) => new Binder(provider));
            this.AddService<ICompiler>((provider, singletons) => new Compiler(provider));
            this.AddService<IDecompiler>((provider, singletons) => new Decompiler(provider));
            this.AddService<ILayoutCreator>((provider, singletons) => new LayoutCreator(provider));
            this.AddService<IPreprocessor>((provider, singletons) => new Preprocessor(provider));
            this.AddService<ILibrarian>((provider, singletons) => new Librarian(provider));
            this.AddService<ILinker>((provider, singletons) => new Linker(provider));
            this.AddService<IResolver>((provider, singletons) => new Resolver(provider));

            this.AddService<ILocalizationParser>((provider, singletons) => new LocalizationParser(provider));
            this.AddService<IVariableResolver>((provider, singletons) => new VariableResolver(provider));
        }

        private Dictionary<Type, Func<IServiceProvider, Dictionary<Type, object>, object>> CreationFunctions { get; }

        private Dictionary<Type, object> Singletons { get; }

        public bool TryGetService(Type serviceType, out object service)
        {
            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            if (!this.Singletons.TryGetValue(serviceType, out service))
            {
                if (this.CreationFunctions.TryGetValue(serviceType, out var creationFunction))
                {
                    service = creationFunction(this, this.Singletons);

#if DEBUG
                    if (!serviceType.IsAssignableFrom(service?.GetType()))
                    {
                        throw new InvalidOperationException($"Creation function for service type: {serviceType.Name} created incompatible service with type: {service?.GetType()}");
                    }
#endif
                }
            }

            return service != null;
        }

        public object GetService(Type serviceType)
        {
            return this.TryGetService(serviceType, out var service) ? service : throw new ArgumentException($"Unknown service type: {serviceType.Name}", nameof(serviceType));
        }

        public void AddService(Type serviceType, Func<IServiceProvider, Dictionary<Type, object>, object> creationFunction)
        {
            this.CreationFunctions[serviceType] = creationFunction;
        }

        public void AddService<T>(Func<IServiceProvider, Dictionary<Type, object>, T> creationFunction)
            where T : class
        {
            this.AddService(typeof(T), creationFunction);
        }

        private static T AddSingleton<T>(Dictionary<Type, object> singletons, T service)
            where T : class
        {
            singletons.Add(typeof(T), service);
            return service;
        }
    }
}
