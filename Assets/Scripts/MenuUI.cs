using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - القائمة الرئيسية (v1.4.0): العنوان + إحصاءات محفوظة
/// + أزرار: بدء السباق، الكراج (اختيار السيارة)، شرح التحكم، إعدادات، خروج + شارة الإصدار.
/// </summary>
public class MenuUI : MonoBehaviour
{
    public static MenuUI Instance { get; private set; }

    RectTransform root;      // الجذر لوحة كاملة (FullRect يعيد RectTransform)
    GameObject howTo;
    Text statsText;

    void Awake()
    {
        Instance = this;
        Build();
        SetVisible(false); // تفتح لاحقاً عبر RaceFlow.Start (EnterMenu)
    }

    public void SetVisible(bool on)
    {
        if (root == null) return;
        root.gameObject.SetActive(on);
        if (!on && howTo != null) howTo.SetActive(false);
    }

    void Build()
    {
        GameObject canvas = UiKit.NewCanvas("MenuCanvas", 40);
        root = UiKit.FullRect(canvas.transform, "Root", UiKit.OverlayDark, raycast: true);

        UiKit.MakeText(root, "Title",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1700f, 150f),
            100, TextAnchor.MiddleCenter, UiKit.Amber, FontStyle.Bold).text = "TURBO RACING";

        UiKit.MakeText(root, "Subtitle",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(1400f, 60f),
            38, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.75f)).text = "REAL 3D RACING";

        statsText = UiKit.MakeText(root, "Stats",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(1500f, 70f),
            42, TextAnchor.MiddleCenter, Color.white);

        UiKit.MakeButton(root, "BtnStart", "START RACE",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -95f), new Vector2(560f, 110f),
            UiKit.Amber, UiKit.BtnAmberText, 52,
            () => { if (RaceFlow.Instance != null) RaceFlow.Instance.StartRace(); });

        // (v1.4.0) الكراج: اختيار السيارة من 6 موديلات بعرض ثلاثي الأبعاد
        UiKit.MakeButton(root, "BtnGarage", "GARAGE",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -215f), new Vector2(560f, 92f),
            new Color(0.85f, 0.55f, 0.10f), UiKit.BtnAmberText, 44,
            () => { if (RaceFlow.Instance != null) RaceFlow.Instance.EnterGarage(); });

        UiKit.MakeButton(root, "BtnHowTo", "HOW TO PLAY",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -318f), new Vector2(560f, 84f),
            UiKit.BtnDark, UiKit.BtnLightText, 38,
            () => { if (howTo != null) howTo.SetActive(true); });

        // (v0.8.0) زر الإعدادات المتقدمة
        UiKit.MakeButton(root, "BtnSettings", "SETTINGS",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -410f), new Vector2(560f, 84f),
            UiKit.BtnDark, UiKit.BtnLightText, 38,
            () => { if (SettingsUI.Instance != null) SettingsUI.Instance.Show(); });

        UiKit.MakeButton(root, "BtnQuit", "QUIT",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -495f), new Vector2(560f, 84f),
            UiKit.BtnDark, UiKit.BtnLightText, 38,
            () => Application.Quit());

        UiKit.MakeText(root, "Version",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, -12f), new Vector2(300f, 40f),
            26, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.55f)).text = "v" + GameVersion.Number;

        BuildHowTo();
    }

    void BuildHowTo()
    {
        // طبقة حاجبة تغطي الشاشة ثم لوحة شرح في المنتصف
        howTo = new GameObject("HowTo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        howTo.transform.SetParent(root, false);

        RectTransform hrt = howTo.GetComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero;
        hrt.anchorMax = Vector2.one;
        hrt.offsetMin = Vector2.zero;
        hrt.offsetMax = Vector2.zero;
        howTo.GetComponent<Image>().color = UiKit.OverlaySoft;
        howTo.SetActive(false);

        RectTransform panel = UiKit.MakePanel(howTo.transform, "Panel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 720f),
            UiKit.PanelDark, raycast: true);

        UiKit.MakeText(panel, "HowTitle",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(1000f, 80f),
            56, TextAnchor.MiddleCenter, UiKit.Amber, FontStyle.Bold).text = "HOW TO PLAY";

        string body =
            "GARAGE: pick one of 6 car models - each has its own top speed,\n" +
            "acceleration and handling (your choice is saved permanently)\n" +
            "RIVALS: 4 AI drivers race against you - your live position (P1/P5)\n" +
            "shows at the top left and the final position in the results\n" +
            "MANUAL DRIVE: hold GAS (big green button, bottom right) to accelerate\n" +
            "Release GAS to coast - the car slows down gently on its own\n" +
            "Hold BRK (red button) to brake; keep holding it to reverse out of tight spots\n" +
            "Steer with the two arrow buttons (bottom left)\n" +
            "Finish 3 laps on the big wide circuit to complete the race\n" +
            "Grass slows you down to 45% speed and reduces grip\n" +
            "Checkpoints will NOT count if you cut across the grass";

        UiKit.MakeText(panel, "HowBody",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1140f, 480f),
            34, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.92f)).text = body;

        UiKit.MakeButton(panel, "BtnClose", "CLOSE",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(320f, 84f),
            UiKit.Amber, UiKit.BtnAmberText, 40,
            () => { if (howTo != null) howTo.SetActive(false); });
    }

    void Update()
    {
        // تحديث الإحصاءات المحفوظة عند ظهور القائمة فقط
        if (root == null || !root.gameObject.activeSelf || statsText == null) return;

        LapManager lm = LapManager.Instance;
        if (lm == null) return;

        statsText.text = "BEST LAP  " + UiKit.FormatTime(lm.BestLapTime) +
                         "        TOTAL LAPS  " + lm.TotalLapsAllTime;
    }
}
