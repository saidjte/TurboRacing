using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - v0.8.0
/// صفحة الإعدادات المتقدمة (كانفاس ترتيب 50 — فوق كل القوائم):
/// أربعة تبويبات:
/// - SOUND: مستوى عام/محرك/مؤثرات + زر تجربة صوت
/// - CONTROLS: حجم الأزرار + شفافيتها + حساسية التوجيه + تبديل الجانبين (يساريون)
/// - CAR: طلاء السيارة (8 ألوان) + مستوى ضبط الأداء (STOCK/SPORT/TURBO)
/// - CAMERA: مسافة الكاميرا + اهتزاز التصادم
/// كل تغيير يُحفظ فوراً (PlayerPrefs) ويُطبَّق فوراً على عالم اللعبة.
/// تُفتح من القائمة الرئيسية ومن قائمة الإيقاف المؤقت.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    public static SettingsUI Instance { get; private set; }

    enum Tab { Sound = 0, Controls = 1, Car = 2, Camera = 3 }

    RectTransform root;
    RectTransform content;
    readonly List<Button> tabButtons = new List<Button>();
    readonly List<Image> swatchRings = new List<Image>();
    readonly List<Image> presetRings = new List<Image>();

    Tab currentTab = Tab.Sound;

    static readonly string[] TabNames = { "SOUND", "CONTROLS", "CAR", "CAMERA" };
    static readonly float[] TabX = { -435f, -145f, 145f, 435f };
    static readonly float[] SwatchX = { -294f, -210f, -126f, -42f, 42f, 126f, 210f, 294f };

    /// <summary>هل الصفحة مفتوحة الآن؟ (يستخدمه RaceFlow لتفادي ESC المزدوج).</summary>
    public bool IsOpen { get { return root != null && root.gameObject.activeSelf; } }

    void Awake()
    {
        Instance = this;
        Build();
        root.gameObject.SetActive(false);
    }

    public void Show()
    {
        GameSettings.EnsureLoaded();
        RefreshTabColors();
        BuildTab();
        if (root != null) root.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (root != null) root.gameObject.SetActive(false);
    }

    // ================= البناء =================

    void Build()
    {
        GameObject canvas = UiKit.NewCanvas("SettingsCanvas", 50);
        root = UiKit.FullRect(canvas.transform, "Root", UiKit.OverlayDark, raycast: true);

        RectTransform panel = UiKit.MakePanel(root, "Panel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(1240f, 880f), UiKit.PanelDark, raycast: true);

        UiKit.MakeText(panel, "Title",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(800f, 80f),
            60, TextAnchor.MiddleCenter, UiKit.Amber, FontStyle.Bold).text = "SETTINGS";

        // زر إغلاق X مرسوم (مستقل عن الخطوط)
        GameObject closeGO = new GameObject("BtnClose",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(panel, false);
        RectTransform crt = closeGO.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(1f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-24f, -24f);
        crt.sizeDelta = new Vector2(72f, 72f);
        Image cimg = closeGO.GetComponent<Image>();
        cimg.sprite = UiKit.CloseSprite();
        cimg.color = Color.white;
        Button cbtn = closeGO.GetComponent<Button>();
        cbtn.targetGraphic = cimg;
        cbtn.onClick.AddListener(Hide);
        cbtn.onClick.AddListener(() =>
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
        });

        // التبويبات الأربعة
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            Button b = UiKit.MakeButton(panel, "Tab" + i, TabNames[i],
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(TabX[i], -150f),
                new Vector2(250f, 70f), UiKit.BtnDark, UiKit.BtnLightText, 34,
                () => SelectTab(idx));
            tabButtons.Add(b);
        }

        // فاصل تحت التبويبات
        UiKit.MakePanel(panel, "Divider",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -215f),
            new Vector2(1140f, 3f), new Color(1f, 1f, 1f, 0.15f), raycast: false);

        // حاوية محتوى التبويب (تُعاد بناؤها عند كل تبديل)
        GameObject contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(panel, false);
        content = contentGO.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0.5f, 0.5f);
        content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.anchoredPosition = new Vector2(0f, -40f);
        content.sizeDelta = new Vector2(1160f, 470f);

        // أزرار سفلية
        UiKit.MakeButton(panel, "BtnReset", "RESET DEFAULTS",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-190f, 50f),
            new Vector2(380f, 84f), UiKit.BtnDark, UiKit.BtnLightText, 34,
            () =>
            {
                GameSettings.ResetDefaults();
                RefreshTabColors();
                BuildTab();
            });

        UiKit.MakeButton(panel, "BtnBack", "BACK",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(190f, 50f),
            new Vector2(380f, 84f), UiKit.Amber, UiKit.BtnAmberText, 34, Hide);
    }

    void SelectTab(int idx)
    {
        currentTab = (Tab)idx;
        RefreshTabColors();
        BuildTab();
    }

    void RefreshTabColors()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            Button b = tabButtons[i];
            if (b == null) continue;

            bool active = (int)currentTab == i;
            b.image.color = active ? UiKit.Amber : UiKit.BtnDark;

            Text t = b.GetComponentInChildren<Text>();
            if (t != null) t.color = active ? UiKit.BtnAmberText : UiKit.BtnLightText;
        }
    }

    void BuildTab()
    {
        // تفريغ المحتوى السابق
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
        swatchRings.Clear();
        presetRings.Clear();

        switch (currentTab)
        {
            case Tab.Sound: BuildSoundTab(); break;
            case Tab.Controls: BuildControlsTab(); break;
            case Tab.Car: BuildCarTab(); break;
            case Tab.Camera: BuildCameraTab(); break;
        }
    }

    // ================= مساعدات الصفوف =================

    void AddLabel2(float y, string s)
    {
        Text t = UiKit.MakeText(content, "Lbl_" + s,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-560f, y),
            new Vector2(400f, 60f), 32, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.92f));
        t.text = s;
    }

    Text AddValue(float y)
    {
        return UiKit.MakeText(content, "Val",
            new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(560f, y),
            new Vector2(200f, 60f), 32, TextAnchor.MiddleRight, UiKit.Amber);
    }

    void AddHint(float y, string s)
    {
        Text t = UiKit.MakeText(content, "Hint",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y),
            new Vector2(1100f, 50f), 27, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f));
        t.text = s;
    }

    /// <summary>صف سلايدر كامل: تسمية + سلايدر + نص قيمة يتحدث حياً.</summary>
    void AddSliderRow(float y, string label,
        float min, float max, System.Func<float> get, System.Action<float> set,
        System.Func<float, string> fmt, System.Action apply)
    {
        AddLabel2(y, label);

        Text val = AddValue(y);
        val.text = fmt(get());

        UiKit.MakeSlider(content, "Sld_" + label,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y),
            new Vector2(620f, 56f), min, max, get(),
            v =>
            {
                set(v);
                GameSettings.Save();
                apply();
                val.text = fmt(v);
            });
    }

    /// <summary>صف زر تبديل ON/OFF (يستخدم للخيارات الثنائية).</summary>
    void AddToggleRow(float y, string label,
        System.Func<bool> get, System.Action<bool> set, System.Action apply)
    {
        AddLabel2(y, label);

        bool on = get();
        Button btn = UiKit.MakeButton(content, "Tgl_" + label, on ? "ON" : "OFF",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y),
            new Vector2(300f, 70f), on ? UiKit.Amber : UiKit.BtnDark,
            on ? UiKit.BtnAmberText : UiKit.BtnLightText, 36, null);

        btn.onClick.AddListener(() =>
        {
            bool nv = !get();
            set(nv);
            GameSettings.Save();
            apply();

            Text t = btn.GetComponentInChildren<Text>();
            if (t != null)
            {
                t.text = nv ? "ON" : "OFF";
                t.color = nv ? UiKit.BtnAmberText : UiKit.BtnLightText;
            }
            if (btn.image != null)
                btn.image.color = nv ? UiKit.Amber : UiKit.BtnDark;
        });
    }

    // ================= التبويبات =================

    void BuildSoundTab()
    {
        AddSliderRow(170f, "MASTER VOLUME", 0f, 1f,
            () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v,
            v => Mathf.RoundToInt(v * 100f) + "%", GameSettings.ApplyAudio);

        AddSliderRow(70f, "ENGINE VOLUME", 0f, 1f,
            () => GameSettings.EngineVolume, v => GameSettings.EngineVolume = v,
            v => Mathf.RoundToInt(v * 100f) + "%", GameSettings.ApplyAudio);

        AddSliderRow(-30f, "EFFECTS VOLUME", 0f, 1f,
            () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v,
            v => Mathf.RoundToInt(v * 100f) + "%", GameSettings.ApplyAudio);

        UiKit.MakeButton(content, "BtnTest", "TEST SOUND",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -130f),
            new Vector2(320f, 76f), UiKit.BtnDark, UiKit.BtnLightText, 34,
            () => { if (SoundManager.Instance != null) SoundManager.Instance.PlayLap(); });

        AddHint(-230f, "Master controls everything - Engine is the motor loop - Effects are beeps and crashes");
    }

    void BuildControlsTab()
    {
        AddSliderRow(170f, "BUTTON SIZE", 0.8f, 1.4f,
            () => GameSettings.BtnScale, v => GameSettings.BtnScale = v,
            v => "x" + v.ToString("0.00"), GameSettings.ApplyButtons);

        AddSliderRow(70f, "BUTTON OPACITY", 0.4f, 1.2f,
            () => GameSettings.BtnOpacity, v => GameSettings.BtnOpacity = v,
            v => "x" + v.ToString("0.00"), GameSettings.ApplyButtons);

        AddSliderRow(-30f, "STEERING SPEED", 0.7f, 1.4f,
            () => GameSettings.SteerSens, v => GameSettings.SteerSens = v,
            v => "x" + v.ToString("0.00"), GameSettings.ApplyCar);

        AddToggleRow(-130f, "SWAP SIDES",
            () => GameSettings.SwapButtons, v => GameSettings.SwapButtons = v,
            GameSettings.ApplyButtons);

        AddHint(-230f, "Swap puts steering on the right and brake on the left (for left-handed thumb)");
    }

    void BuildCarTab()
    {
        // ===== الطلاء (8 ألوان) =====
        AddLabel2(170f, "PAINT");

        for (int i = 0; i < GameSettings.CarColors.Length; i++)
        {
            int idx = i;

            // حلقة تحديد بيضاء خلف العينة (تظهر فقط عند الاختيار)
            RectTransform ring = UiKit.MakePanel(content, "Ring" + i,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(SwatchX[i], 170f),
                new Vector2(82f, 82f), Color.white, raycast: false);
            ring.GetComponent<Image>().sprite = UiKit.CircleSprite();
            ring.gameObject.SetActive(idx == GameSettings.CarColorIndex);
            swatchRings.Add(ring.GetComponent<Image>());

            UiKit.MakeButton(content, "Swatch" + i, "",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(SwatchX[i], 170f),
                new Vector2(64f, 64f), GameSettings.CarColors[i], Color.white, 20,
                () =>
                {
                    GameSettings.CarColorIndex = idx;
                    GameSettings.Save();
                    GameSettings.ApplyCarColor();
                    RefreshSwatches();
                });
        }

        // ===== مستوى الضبط =====
        AddLabel2(70f, "TUNING");

        for (int i = 0; i < GameSettings.PresetNames.Length; i++)
        {
            int idx = i;

            RectTransform ring = UiKit.MakePanel(content, "Pring" + i,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 210f, 70f),
                new Vector2(202f, 80f), Color.white, raycast: false);
            ring.gameObject.SetActive(idx == GameSettings.CarPreset);
            presetRings.Add(ring.GetComponent<Image>());

            UiKit.MakeButton(content, "Preset" + i, GameSettings.PresetNames[i],
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 210f, 70f),
                new Vector2(190f, 70f), UiKit.BtnDark, UiKit.BtnLightText, 34,
                () =>
                {
                    GameSettings.CarPreset = idx;
                    GameSettings.Save();
                    GameSettings.ApplyCar();
                    RefreshPresets();
                });
        }

        AddHint(-30f, "STOCK: balanced  |  SPORT: faster and grippier  |  TURBO: fastest but driftier");

        // عرض حي لأرقام المستوى المختار
        Text live = UiKit.MakeText(content, "TuningStats",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -130f),
            new Vector2(1100f, 50f), 30, TextAnchor.MiddleCenter, UiKit.Amber);
        live.text = "TOP SPEED " + TopSpeedText() + "  -  ACCEL " + AccelText() +
                    "  -  TURN " + TurnText() + "  -  GRIP " + GripText();
    }

    string TopSpeedText()
    {
        int p = Mathf.Clamp(GameSettings.CarPreset, 0, 2);
        float v = p == 1 ? 15.5f : p == 2 ? 17f : 14f;
        return Mathf.RoundToInt(v * 3.6f) + " KM/H";
    }

    string AccelText()
    {
        int p = Mathf.Clamp(GameSettings.CarPreset, 0, 2);
        return "x" + (p == 1 ? 1.2f : p == 2 ? 1.4f : 1f).ToString("0.0");
    }

    string TurnText()
    {
        int p = Mathf.Clamp(GameSettings.CarPreset, 0, 2);
        return "x" + (p == 1 ? 1.07f : p == 2 ? 1.11f : 1f).ToString("0.00");
    }

    string GripText()
    {
        int p = Mathf.Clamp(GameSettings.CarPreset, 0, 2);
        return p == 2 ? "DRIFTY" : p == 1 ? "HIGH" : "NORMAL";
    }

    void BuildCameraTab()
    {
        AddSliderRow(170f, "CAMERA DISTANCE", 0.8f, 1.3f,
            () => GameSettings.CamZoom, v => GameSettings.CamZoom = v,
            v => "x" + v.ToString("0.00"), GameSettings.ApplyCamera);

        AddToggleRow(70f, "CRASH SHAKE",
            () => GameSettings.CamShake, v => GameSettings.CamShake = v,
            GameSettings.ApplyCamera);

        AddHint(-30f, "Distance widens the chase camera - shake adds impact feedback on collisions");
    }

    void RefreshSwatches()
    {
        for (int i = 0; i < swatchRings.Count; i++)
        {
            if (swatchRings[i] != null)
                swatchRings[i].gameObject.SetActive(i == GameSettings.CarColorIndex);
        }
    }

    void RefreshPresets()
    {
        for (int i = 0; i < presetRings.Count; i++)
        {
            if (presetRings[i] != null)
                presetRings[i].gameObject.SetActive(i == GameSettings.CarPreset);
        }
    }

    // ================= إدخال =================

    void Update()
    {
        // ESC يغلق الإعدادات فقط (RaceFlow يتجاهل ESC ما دامت الصفحة مفتوحة)
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }
}
