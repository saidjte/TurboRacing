using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - المرحلة 4
/// القائمة الرئيسية: العنوان + إحصاءات محفوظة (أفضل لفة/إجمالي اللفات)
/// + أزرار: بدء السباق، شرح التحكم، خروج + شارة الإصدار.
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
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1700f, 180f),
            120, TextAnchor.MiddleCenter, UiKit.Amber, FontStyle.Bold).text = "TURBO RACING";

        UiKit.MakeText(root, "Subtitle",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -345f), new Vector2(1400f, 60f),
            38, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.75f)).text = "REAL 3D RACING";

        statsText = UiKit.MakeText(root, "Stats",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -430f), new Vector2(1500f, 70f),
            44, TextAnchor.MiddleCenter, Color.white);

        UiKit.MakeButton(root, "BtnStart", "START RACE",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(560f, 120f),
            UiKit.Amber, UiKit.BtnAmberText, 56,
            () => { if (RaceFlow.Instance != null) RaceFlow.Instance.StartRace(); });

        UiKit.MakeButton(root, "BtnHowTo", "HOW TO PLAY",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), new Vector2(560f, 88f),
            UiKit.BtnDark, UiKit.BtnLightText, 42,
            () => { if (howTo != null) howTo.SetActive(true); });

        // (v0.8.0) زر الإعدادات المتقدمة
        UiKit.MakeButton(root, "BtnSettings", "SETTINGS",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(560f, 88f),
            UiKit.BtnDark, UiKit.BtnLightText, 42,
            () => { if (SettingsUI.Instance != null) SettingsUI.Instance.Show(); });

        UiKit.MakeButton(root, "BtnQuit", "QUIT",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -410f), new Vector2(560f, 88f),
            UiKit.BtnDark, UiKit.BtnLightText, 42,
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
            "The car accelerates on its own - hold the arrow buttons (bottom left) to steer\n" +
            "BRK (bottom right) brakes. Keep holding it to reverse out of tight spots\n" +
            "Finish 3 laps to complete the race\n" +
            "Grass slows you down to 45% speed\n" +
            "Checkpoints will NOT count if you cut across the grass\n" +
            "Your best lap is saved automatically\n" +
            "SETTINGS: tune sound, buttons, paint and camera\n" +
            "Tap the pause button (top right) or press ESC / Back to pause";

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
