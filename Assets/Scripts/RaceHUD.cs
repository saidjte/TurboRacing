using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - المرحلة 3
/// واجهة السباق (HUD) مبنية بالكود بالكامل:
/// - عدد اللفات (يسار أعلى) + زمن اللفة الحالية (وسط أعلى) + الأفضل/الأخير (يمين أعلى)
/// - شارة إصدار اللعبة صغيرة أسفل يمين الشاشة (تقرأ من GameVersion)
/// - عرض فقط: لا يلتقط اللمس (raycastTarget = false) حتى لا يعيق أزرار القيادة
/// </summary>
public class RaceHUD : MonoBehaviour
{
    Text lapText;
    Text timeText;
    Text bestText;
    Text lastText;

    void Start()
    {
        BuildCanvas();
    }

    void BuildCanvas()
    {
        GameObject canvasGO = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        lapText  = MakeText(canvasGO.transform, "LapText",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -30f),
            new Vector2(500f, 90f), 64, TextAnchor.UpperLeft);

        timeText = MakeText(canvasGO.transform, "TimeText",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f),
            new Vector2(700f, 110f), 84, TextAnchor.UpperCenter);

        bestText = MakeText(canvasGO.transform, "BestText",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -30f),
            new Vector2(700f, 60f), 44, TextAnchor.UpperRight);

        lastText = MakeText(canvasGO.transform, "LastText",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -96f),
            new Vector2(700f, 60f), 44, TextAnchor.UpperRight);

        Text versionText = MakeText(canvasGO.transform, "VersionText",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, -12f),
            new Vector2(300f, 40f), 26, TextAnchor.LowerRight);
        versionText.text = "v" + GameVersion.Number;
        versionText.color = new Color(1f, 1f, 1f, 0.55f);
    }

    Text MakeText(Transform parent, string name,
        Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size,
        int fontSize, TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        // حدود سوداء للقراءة فوق أي لون في الحلبة
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        return text;
    }

    void Update()
    {
        LapManager lm = LapManager.Instance;
        if (lm == null) return;

        lapText.text  = "LAP " + (lm.LapsCompleted + 1);
        timeText.text = FormatTime(lm.CurrentLapTime);
        bestText.text = "BEST " + FormatTime(lm.BestLapTime);
        lastText.text = lm.LastLapTime > 0f ? "LAST " + FormatTime(lm.LastLapTime) : "";
    }

    static string FormatTime(float t)
    {
        if (t <= 0f) return "--:--.---";

        int ms = Mathf.RoundToInt(t * 1000f);
        int minutes = ms / 60000; ms -= minutes * 60000;
        int seconds = ms / 1000;  ms -= seconds * 1000;
        return $"{minutes:00}:{seconds:00}.{ms:000}";
    }
}
