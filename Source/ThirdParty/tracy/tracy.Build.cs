// Copyright (c) 2012-2023 Wojciech Figat. All rights reserved.

using System.Collections.Generic;
using System.IO;
using Flax.Build;
using Flax.Build.NativeCpp;

/// <summary>
/// https://github.com/wolfpld/tracy
/// </summary>
public class tracy : ThirdPartyModule
{
    /// <summary>
    /// Enables on-demand profiling.
    /// </summary>
    public static bool OnDemand = true;

    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        LicenseType = LicenseTypes.BSD3Clause;
        LicenseFilePath = "LICENSE";

        // Merge third-party modules into engine binary
        BinaryModuleName = "FlaxEngine";
    }

    /// <inheritdoc />
    public override void Setup(BuildOptions options)
    {
        base.Setup(options);

        options.SourcePaths.Clear();
        options.SourceFiles.Clear();
        options.SourceFiles.Add(Path.Combine(FolderPath, "Tracy.h"));
        options.SourceFiles.Add(Path.Combine(FolderPath, "TracyClient.cpp"));

        options.PublicDefinitions.Add("TRACY_ENABLE");
        options.PrivateDefinitions.Add("TRACY_NO_INVARIANT_CHECK");
        if (options.Platform.Target == TargetPlatform.Windows)
        {
            options.PrivateDefinitions.Add("TRACY_DBGHELP_LOCK=DbgHelp");
        }
        if (OnDemand)
        {
            options.PublicDefinitions.Add("TRACY_ON_DEMAND");
        }
    }

    /// <inheritdoc />
    public override void GetFilesToDeploy(List<string> files)
    {
        base.GetFilesToDeploy(files);

        files.Add(Path.Combine(FolderPath, "Tracy.h"));
        files.Add(Path.Combine(FolderPath, "common", "TracySystem.hpp"));
        files.Add(Path.Combine(FolderPath, "client", "TracyCallstack.h"));
    }
}
