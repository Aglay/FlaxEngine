// Copyright (c) 2012-2023 Wojciech Figat. All rights reserved.

using System.IO;
using Flax.Build;
using Flax.Build.NativeCpp;

/// <summary>
/// https://github.com/zeux/volk
/// </summary>
public class volk : ThirdPartyModule
{
    private bool _missingSDKError;

    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        LicenseType = LicenseTypes.MIT;
        LicenseFilePath = "LICENSE.txt";

        // Merge third-party modules into engine binary
        BinaryModuleName = "FlaxEngine";
    }

    /// <inheritdoc />
    public override void Setup(BuildOptions options)
    {
        base.Setup(options);

        switch (options.Platform.Target)
        {
        case TargetPlatform.Windows:
            options.PublicDefinitions.Add("VK_USE_PLATFORM_WIN32_KHR");
            break;
        case TargetPlatform.Linux:
            options.PrivateDefinitions.Add("VK_USE_PLATFORM_XLIB_KHR");
            break;
        case TargetPlatform.Android:
            options.PublicDefinitions.Add("VK_USE_PLATFORM_ANDROID_KHR");
            break;
        case TargetPlatform.Mac:
            options.PublicDefinitions.Add("VK_USE_PLATFORM_MACOS_MVK");
            options.DependencyFiles.Add(Path.Combine(VulkanSdk.Instance.RootPath, "../MoltenVK/dylib/macOS/libMoltenVK.dylib"));
            options.DependencyFiles.Add(Path.Combine(VulkanSdk.Instance.RootPath, "../MoltenVK/dylib/macOS/MoltenVK_icd.json"));
            break;
        default: throw new InvalidPlatformException(options.Platform.Target);
        }

        string includesFolderPath;
        if (VulkanSdk.Instance.TryGetIncludePath(out includesFolderPath))
        {
            options.PublicIncludePaths.Add(includesFolderPath);
        }
        else
        {
            Log.ErrorOnce("Missing VulkanSDK.", ref _missingSDKError);
        }
    }
}
