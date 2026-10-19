using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - v1.4.0 (الكراج)
/// واجهة اختيار السيارة: اسم الموديل + الأصل الملهم + أشرطة إحصاءات مقسّمة
/// (سرعة/تسارع/توجيه) + سهمَا تنقّل يبدّلان العرض ثلاثي الأبعاد على المنصة
/// + زر اختيار يحفظ الاختيار دائمياً (PlayerPrefs) + زر عودة للقائمة.
/// النموذج يُبنى في المشهد (GarageStage) — هذه الواجهة طبقة تحكم فقط.
/// </summary>
public class GarageUI : MonoBehaviour
{
    public static GarageUI Instance { get; private set; }

    RectTransform root;
    Text nameText;
    Text tagText;
    Text indexText;
    Text savedText;

    // ثلاثة صفوف إحصاءات × 10 خانات
    Image[][] bars;
    readonly string[] statLabels = { "SPEED", "ACCEL", "GRIP" };

    int viewIdx;
    float savedFlashUntil;

    void Awake()
    {
        Instance = this;
        Build();
        SetVisible(false);
    }

    public void SetVisible(bool on)
    {
        if (root == null) return;
        root.gameObject.SetActive(on);
        if (on) SyncFromSettings();
    }

    void Build()
    {
        GameObject canvas = UiKit.NewCanvas("GarageCanvas", 45);
        root = UiKit.FullRect(canvas.transform, "Root", new Color(0.02f, 0.02f, 0.04f, 0.80f), raycast: true);

        // ================= العنوان =================
        UiKit.MakeText(root, "Title",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1200f, 110f),
            84, TextAnchor.MiddleCenter, UiKit.Amber, FontStyle.Bold).text = "GARAGE";

        indexText = UiKit.MakeText(root, "Index",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(400f, 50f),
            34, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.65f));

        // ================= اسم السيارة + الأصل الملهم =================
        nameText = UiKit.MakeText(root, "CarName",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 205f), new Vector2(1000f, 80f),
            60, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);

        tagText = UiKit.MakeText(root, "CarTag",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 152f), new Vector2(1000f, 44f),
            28, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.60f));

        // ================= أشرطة الإحصاءات (يمين الشاشة) =================
        RectTransform statsPanel = UiKit.MakePanel(root, "StatsPanel",
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-36f, 40f), new Vector2(430f, 330f),
            UiKit.PanelDark, raycast: true);

        UiKit.MakeText(statsPanel, "StatsTitle",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(380f, 50f),
            34, TextAnchor.MiddleCenter, UiKit.Amber, FontStyle.Bold).text = "SPECS";

        bars = new Image[3][];
        for (int row = 0; row < 3; row++)
        {
            float y = -105f - row * 78f;

            UiKit.MakeText(statsPanel, "Label" + row,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(380f, 40f),
                26, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f)).text = statLabels[row];

            bars[row] = new Image[10];
            float cellW = 26f, gap = 6f;
            float totalW = 10 * cellW + 9 * gap;
            for (int i = 0; i < 10; i++)
            {
                float x = -totalW * 0.5f + cellW * 0.5f + i * (cellW + gap);
                RectTransform cell = UiKit.MakePanel(statsPanel, "Bar" + row + "_" + i,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, y - 34f),
                    new Vector2(cellW, 24f), new Color(1f, 1f, 1f, 0.14f), raycast: false);
                bars[row][i] = cell.GetComponent<Image>();
            }
        }

        // ================= أسهم التنقل (أيقونات مرسومة — مستقلة عن الخطوط) =================
        MakeArrowButton(root, "BtnPrev", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(150f, 0f), pointRight: false,
            () => Step(-1));

        MakeArrowButton(root, "BtnNext", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-150f, 0f), pointRight: true,
            () => Step(+1));

        UiKit.MakeText(root, "HintArrows",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(900f, 44f),
            26, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.45f)).text = "BROWSE MODELS";

        // ================= زرا الاختيار والعودة =================
        UiKit.MakeButton(root, "BtnSelect", "SELECT",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(480f, 104f),
            UiKit.Amber, UiKit.BtnAmberText, 52,
            OnSelect);

        savedText = UiKit.MakeText(root, "SavedFlash",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -15f), new Vector2(600f, 40f),
            30, TextAnchor.MiddleCenter, UiKit.Amber, FontStyle.Bold);
        savedText.text = "";

        UiKit.MakeButton(root, "BtnBack", "BACK",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(230f, 84f),
            UiKit.BtnDark, UiKit.BtnLightText, 38,
            () => { if (RaceFlow.Instance != null) RaceFlow.Instance.ExitGarage(); });

        UiKit.MakeText(root, "Version",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, -12f), new Vector2(300f, 40f),
            26, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.55f)).text = "v" + GameVersion.Number;
    }

    /// <summary>زر سهم بأيقونة مرسومة (نفس منهجية أزرار القيادة المحصّنة ضد أعطال الخطوط).</summary>
    static void MakeArrowButton(Transform parent, string name, Vector2 anchor, Vector2 pivot,
        Vector2 pos, bool pointRight, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(120f, 120f);

        Image img = go.GetComponent<Image>();
        img.color = UiKit.BtnDark;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(onClick);
        btn.onClick.AddListener(() =>
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
        });

        // الأيقونة: صورة مركزية لا تعترض اللمس
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(go.transform, false);

        RectTransform irt = iconGO.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 0.5f);
        irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = Vector2.zero;
        irt.sizeDelta = new Vector2(56f, 56f);

        Image icon = iconGO.GetComponent<Image>();
        icon.sprite = UiKit.TriangleSprite(pointRight);
        icon.color = UiKit.Amber;
        icon.raycastTarget = false;
    }

    void Step(int dir)
    {
        viewIdx = (viewIdx + dir + CarModels.Count) % CarModels.Count;
        if (GarageStage.Instance != null) GarageStage.Instance.Show(viewIdx);
        UpdateLabels();
    }

    void OnSelect()
    {
        GameSettings.SelectedCarModel = viewIdx;
        GameSettings.Save();
        savedFlashUntil = Time.time + 1.4f;
        savedText.text = "SELECTED - " + CarModels.Get(viewIdx).name + "!";
    }

    void SyncFromSettings()
    {
        viewIdx = GameSettings.SelectedCarModel;
        if (GarageStage.Instance != null) GarageStage.Instance.Show(viewIdx);
        UpdateLabels();
        savedText.text = "";
    }

    void UpdateLabels()
    {
        CarModels.CarSpec spec = CarModels.Get(viewIdx);

        nameText.text = spec.name;
        tagText.text = "INSPIRED BY:  " + spec.tag;
        indexText.text = (viewIdx + 1) + " / " + CarModels.Count;

        // القيم النسبية للأشرطة (0..1)
        float[] vals =
        {
            Mathf.Clamp01(spec.topSpeed / 95f),
            Mathf.Clamp01(spec.accel / 10f),
            Mathf.Clamp01(spec.steerAngle / 45f),
        };

        for (int row = 0; row < 3; row++)
        {
            int filled = Mathf.RoundToInt(vals[row] * 10f);
            for (int i = 0; i < 10; i++)
                bars[row][i].color = i < filled
                    ? UiKit.Amber
                    : new Color(1f, 1f, 1f, 0.14f);
        }
    }

    void Update()
    {
        if (savedText == null) return;
        if (savedText.gameObject.activeSelf && Time.time > savedFlashUntil)
            savedText.text = "";
    }
}
