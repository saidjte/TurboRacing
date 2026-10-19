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
        camGO.transform.position = new Vector3(46f, 80f, -20f); // فوق الحلبة (GameBootstrap يعيد الضبط)
        camGO.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

        // v1.0.0: كاميرا منظورية حقيقية للعالم المجسّم
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = 58f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 600f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.62f, 0.76f, 0.94f);
        camGO.AddComponent<AudioListener>();

        new GameObject("GameBootstrap").AddComponent<GameBootstrap>();

        EnsureShadersIncluded();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[BuildCommand] تم إنشاء المشهد وحفظه: " + ScenePath);
    }

    /// <summary>
    /// v1.0.0: ضمان تضمين الشيدرات المستخدمة وقت التشغيل في البناء — المشهد
    /// يُنشأ برمجياً فارغاً لذا لن يكتشف Unity الشيدرات تلقائياً.
    /// 1) نكتب شيدر TR/Paint المخصص (ألوان رؤوس + Lambert + BlinnPhong + ظلال)
    ///    داخل مجلد Resources (يُدرج كاملاً في حزمة اللاعب دائماً).
    /// 2) مادة مرجعية Standard كخطة بديلة.
    /// 3) كائن مرساة معطّل في المشهد نفسه (طبقة أمان إضافية).
    /// </summary>
    private static void EnsureShadersIncluded()
    {
        Directory.CreateDirectory("Assets/Resources");

        // 1) شيدر TR/Paint المخصص — دعم مضمون لألوان الرؤوس مع الظلال
        var shaderPath = "Assets/Resources/TR_Paint.shader";
        if (!File.Exists(shaderPath))
        {
            File.WriteAllText(shaderPath, TrPaintShaderSource);
            AssetDatabase.ImportAsset(shaderPath);
            Debug.Log("[BuildCommand] تم توليد شيدر TR/Paint داخل Resources");
        }

        // 2) مادة Standard مرجعية (خطة بديلة إذا فشل Find للشيدر المخصص)
        Shader std = Shader.Find("Standard");
        if (std != null)
        {
            var matPath = "Assets/Resources/TR_StandardMat.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing == null)
            {
                var mat = new Material(std) { name = "TR_StandardMat" };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                existing.shader = std;
            }

            // 3) مرساة مشهد معطّلة (لا تُرَصَّد ولا تُرسم)
            var anchor = new GameObject("ShaderAnchor");
            anchor.SetActive(false);
            var mr = anchor.AddComponent<MeshRenderer>();
            mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }
    }

    /// <summary>
    /// مصدر شيدر TR/Paint: Surface shader يدعم ألوان الرؤوس (COLOR semantic —
    /// ميزة موثقة في Surface Shaders) + ظلال تلقائية + BlinnPhong خفيف للموبايل
    /// (noforwardadd = ضوء اتجاهي واحد فقط). النسيج أبيض والمواد تضبط المواصفات.
    /// </summary>
    private const string TrPaintShaderSource = @"
Shader ""TR/Paint""
{
    Properties
    {
        _MainTex (""Texture"", 2D) = ""white"" {}
        _SpecColor (""Specular Color"", Color) = (0.15, 0.15, 0.15, 1)
        _Shininess (""Shininess"", Range(0.01, 1)) = 0.08
    }
    SubShader
    {
        Tags { ""RenderType""=""Opaque"" }
        LOD 200

        CGPROGRAM
        #pragma surface surf BlinnPhong noforwardadd
        #pragma target 2.0

        sampler2D _MainTex;
        half _Shininess;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * IN.color;
            o.Albedo = c.rgb;
            o.Specular = 1.0;
            o.Shininess = _Shininess;
            o.Alpha = c.a;
        }
        ENDCG
    }
    Fallback ""Diffuse""
}
";
}
