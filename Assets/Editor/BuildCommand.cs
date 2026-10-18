using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// أمر البناء المخصص لبيئة CI (GameCI).
/// ينشئ المشهد برمجياً، يضبط إعدادات أندرويد، ثم يبني ملف APK.
/// اسم الملف يحمل رقم النسخة وجوباً (GameVersion) — القاعدة: كل إصلاح/ترقية يرفع الرقم.
/// يُستدعى عبر مدخل buildMethod: BuildCommand.PerformBuild في unity-builder@v4.
/// </summary>
public static class BuildCommand
{
    private const string ScenePath = "Assets/Scenes/Game.unity";
    private const string OutputPath = "build/Android/TurboRacing_v" + GameVersion.Number + ".apk";

    public static void PerformBuild()
    {
        Debug.Log("[BuildCommand] بدء البناء...");

        ConfigureAndroidPlayer();
        CreateAndSaveScene();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        var report = BuildPipeline.BuildPlayer(
            new[] { ScenePath },
            OutputPath,
            BuildTarget.Android,
            BuildOptions.None);

        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception($"[BuildCommand] فشل البناء: {report.summary.result} (عدد الأخطاء: {report.summary.totalErrors})");

        Debug.Log($"[BuildCommand] تم البناء بنجاح: {OutputPath} بحجم {report.summary.totalSize} بايت");
    }

    private static void ConfigureAndroidPlayer()
    {
        PlayerSettings.companyName = "TurboRacing";
        PlayerSettings.productName = "TurboRacing";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.turboracing.game");
        PlayerSettings.bundleVersion = GameVersion.Number;              // نسخة مرئية في إعدادات النظام
        PlayerSettings.Android.bundleVersionCode = GameVersion.Code;    // رمز متزايد للتحديثات

        // الوضع الأفقي فقط (يسار/يمين)
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

        // IL2CPP مطلوب لدعم ARM64 (Mono لا يدعمه على أندرويد)
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

        Debug.Log("[BuildCommand] تم ضبط إعدادات اللاعب لأندرويد (IL2CPP, ARMv7+ARM64)");
    }

    private static void CreateAndSaveScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0f, 0f, -10f);

        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 7f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.18f, 0.20f);
        camGO.AddComponent<AudioListener>();

        new GameObject("GameBootstrap").AddComponent<GameBootstrap>();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[BuildCommand] تم إنشاء المشهد وحفظه: " + ScenePath);
    }
}
