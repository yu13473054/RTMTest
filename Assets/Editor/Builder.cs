using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

public class Builder : IPostprocessBuildWithReport
{
    /// 获取添加场景
    static string[] GetBuildScenes()
    {
        List<string> names = new List<string>();
        foreach (EditorBuildSettingsScene e in EditorBuildSettings.scenes)
        {
            if (e == null)
                continue;
            if (e.enabled)
                names.Add(e.path);
        }
        return names.ToArray();
    }
    [MenuItem("Tools/Build Android")]
    public static void BuildAndroid()
    {
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        BuildPlayerOptions opts = new BuildPlayerOptions();
        string apkName = "test.apk";
        opts.locationPathName = "Android/"+ apkName;
        opts.scenes = GetBuildScenes();
        opts.target = EditorUserBuildSettings.activeBuildTarget;
        BuildPipeline.BuildPlayer(opts);
    }
    [MenuItem("Tools/Build iOS")]
    public static void BuildIOS()
    {
        //删除旧的导出工程
        string exportPath = System.Environment.CurrentDirectory+ "/iOSBuild";
        if (Directory.Exists(exportPath))
            UnityEditor.FileUtil.DeleteFileOrDirectory(exportPath);

        PlayerSettings.iOS.hideHomeButton = true;
        PlayerSettings.iOS.appleEnableAutomaticSigning = false;

        //证书
        PlayerSettings.iOS.iOSManualProvisioningProfileType = ProvisioningProfileType.Development;
        PlayerSettings.iOS.iOSManualProvisioningProfileID = "acd03fe5-6e43-4aca-9ec1-d582c641201f";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.gmaker.cookingappletest");
        
        BuildPlayerOptions opts = new BuildPlayerOptions();
        opts.scenes = GetBuildScenes();
        opts.locationPathName = "iOSBuild";
        opts.target = EditorUserBuildSettings.activeBuildTarget;
        BuildPipeline.BuildPlayer(opts);
    }

    //设置Capabilities
    void SetCapabilities(string pathToBuildProject)
    {
        string projPath = pathToBuildProject + "/Unity-iPhone.xcodeproj/project.pbxproj"; //项目路径，这个路径在mac上默认是不显示的，需要右键->显示包内容才能看到。unity到处的名字就是这个。
        UnityEditor.iOS.Xcode.PBXProject pbxProj = new UnityEditor.iOS.Xcode.PBXProject();//创建xcode project类
        pbxProj.ReadFromString(File.ReadAllText(projPath));//xcode project读入
        string targetGuid = pbxProj.GetUnityMainTargetGuid();//获得Target名

        //设置BuildSetting
        // pbxProj.SetBuildProperty(targetGuid, "ENABLE_BITCODE", "NO");
        // pbxProj.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-ObjC");
        // pbxProj.SetBuildProperty(targetGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym"); //定位崩溃bug
        // pbxProj.SetBuildProperty(targetGuid, "EXCLUDED_ARCHS", "armv7");
        //
        // pbxProj.AddFrameworkToProject(targetGuid, "MediaPlayer.framework", false);
        // pbxProj.AddFrameworkToProject(targetGuid, "AdSupport.framework", true);
        // pbxProj.AddFrameworkToProject(targetGuid, "GameKit.framework", false);
        // pbxProj.AddFrameworkToProject(targetGuid, "AssetsLibrary.framework", false);
        // pbxProj.AddFrameworkToProject(targetGuid, "WebKit.framework", true);
        // pbxProj.AddFrameworkToProject(targetGuid, "CoreTelephony.framework", false);
        // pbxProj.AddFrameworkToProject(targetGuid, "StoreKit.framework", false);
        // pbxProj.AddFrameworkToProject(targetGuid, "SafariServices.framework", false);
        // pbxProj.AddFrameworkToProject(targetGuid, "libsqlite3.tbd", false);
        // pbxProj.AddFrameworkToProject(targetGuid, "libc++.tbd", false);
        // pbxProj.AddFrameworkToProject(targetGuid, "libz.tbd", false);
        // pbxProj.AddFrameworkToProject(targetGuid, "VideoToolbox.framework", false);
        // pbxProj.AddFrameworkToProject(targetGuid, "Accelerate.framework", false);
        // pbxProj.AddFrameworkToProject(targetGuid, "LocalAuthentication.framework", false);
        // pbxProj.AddFrameworkToProject(targetGuid, "AuthenticationServices.framework", false);
        // pbxProj.AddFrameworkToProject(targetGuid, "libresolv.tbd", false);


        //修改编译方式
        // string mmfile = pbxProj.FindFileGuidByProjectPath("Classes/UnityAppController.mm");
        // var flags = pbxProj.GetCompileFlagsForFile(targetGuid, mmfile);
        // flags.Add("-fno-objc-arc");
        // pbxProj.SetCompileFlagsForFile(targetGuid, mmfile, flags);
        // mmfile = pbxProj.FindFileGuidByProjectPath("Libraries/Plugins/IOS/LTSDK/LTSDKNPC.mm");
        // flags = pbxProj.GetCompileFlagsForFile(targetGuid, mmfile);
        // flags.Add("-fno-objc-arc");
        // pbxProj.SetCompileFlagsForFile(targetGuid, mmfile, flags);
        // pbxProj.WriteToFile(projPath);

        //设置Capability
        // string[] splits = PlayerSettings.applicationIdentifier.Split('.');
        // var capManager = new ProjectCapabilityManager(projPath, splits[splits.Length - 1] + ".entitlements", pbxProj.GetUnityMainTargetGuid());//创建设置Capability类
        // capManager.AddInAppPurchase();
        // capManager.AddSignInWithApple();
        // capManager.WriteToFile();//写入文件保存
    }

    //设置info.plist文件
    void SetInfo(string pathToBuildProject)
    {
        string plistPath = pathToBuildProject + "/Info.plist";
        PlistDocument plist = new PlistDocument();
        plist.ReadFromString(File.ReadAllText(plistPath));
        PlistElementDict plistRoot = plist.root;
        //
        // //指定打包时使用的ProvisioningProfile
        // PlistElementDict dict = plistRoot.CreateDict("provisioningProfiles");
        // dict.SetString(PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS), PlayerSettings.iOS.iOSManualProvisioningProfileID);
        // plistRoot.SetString("method", PlayerSettings.iOS.iOSManualProvisioningProfileType == ProvisioningProfileType.Development ? "development" : "app-store");
        //
        // plistRoot.SetString("NSCameraUsageDescription", "需要使用相机");
        // plistRoot.SetString("NSCalendarsUsageDescription", "需要使用日历");
        plistRoot.SetString("NSPhotoLibraryUsageDescription", "需要使用相册");
        // plistRoot.SetString("NSLocationWhenInUseUsageDescription", "需要访问地理位置");
        File.WriteAllText(plistPath, plist.WriteToString());
    }

    void OnPostprocessBuildIOS(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS)
            return;
        string pathToBuildProject = report.summary.outputPath;
        SetCapabilities(pathToBuildProject);
        SetInfo(pathToBuildProject);

        //替换mm文件
        // string targetPath = pathToBuildProject + "/Classes/UnityAppController.mm";
        // if (File.Exists(targetPath)) UnityEditor.FileUtil.DeleteFileOrDirectory(targetPath);
        // UnityEditor.FileUtil.CopyFileOrDirectory(System.Environment.CurrentDirectory + "/LTBaseSDK_Oversea/UnityAppController.mm", targetPath);
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        OnPostprocessBuildIOS(report);
    }
    public int callbackOrder
    {
        get { return 1; }
    }
}