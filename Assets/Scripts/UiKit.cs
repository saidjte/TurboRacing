using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - المرحلة 4
/// أدوات واجهة مشتركة: كل الواجهات تُبنى بالكود عبر هذه المساعدة
/// (كانفاس بمقاس مرجعي موحّد + نصوص + أزرار + لوحات + تنسيق وقت موحّد).
/// </summary>
public static class UiKit
{
    // ---- الهوية البصرية الموحّدة ----
    public static readonly Color Amber = new Color(1f, 0.72f, 0.15f);      // لون السباق الأساسي
    public static readonly Color PanelDark = new Color(0.06f, 0.07f, 0.09f, 0.96f);
    public static readonly Color OverlayDark = new Color(0f, 0f, 0f, 0.60f);
    public static readonly Color OverlaySoft = new Color(0f, 0f, 0f, 0.45f);
    public static readonly Color BtnDark = new Color(0f, 0f, 0f, 0.55f);
    public static readonly Color BtnLightText = Color.white;
    public static readonly Color BtnAmberText = new Color(0.12f, 0.08f, 0.02f);

    public static Font BuiltinFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    /// <summary>كانفاس Overlay بمقاس مرجعي 1920×1080 (مطابق لعرض الهواتف الأفقية).</summary>
    public static GameObject NewCanvas(string name, int sortingOrder)
    {
        GameObject go = new GameObject(name,
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        return go;
    }

    /// <summary>لوحة تغطي الشاشة كاملة (خلفية معتمة تحجب اللمس عن ما تحتها).</summary>
    public static RectTransform FullRect(Transform parent, string name, Color bg, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = bg;
        img.raycastTarget = raycast;
        return rt;
    }

    /// <summary>لوحة بحجم وموضع محدد (نقطة ارتكاز واحدة).</summary>
    public static RectTransform MakePanel(Transform parent, string name,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return rt;
    }

    /// <summary>نص بحدود سوداء للقراءة فوق أي خلفية (raycast معطل دائماً).</summary>
    public static Text MakeText(Transform parent, string name,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        int fontSize, TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Text text = go.GetComponent<Text>();
        text.font = BuiltinFont;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.fontStyle = style;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        return text;
    }

    /// <summary>زر كامل (صورة + نص + حدث ضغط) بنمط موحّد.</summary>
    public static Button MakeButton(Transform parent, string name, string label,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        Color bg, Color fg, int fontSize, UnityEngine.Events.UnityAction onClick,
        FontStyle style = FontStyle.Bold)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.color = bg;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(onClick);

        // نص ممتد يملأ الزر
        Text text = MakeText(go.transform, "Label",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size,
            fontSize, TextAnchor.MiddleCenter, fg, style);
        RectTransform trt = text.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        trt.anchoredPosition = Vector2.zero;

        return btn;
    }

    /// <summary>تنسيق موحّد للوقت: mm:ss.mmm (أو شرطات عندما لا يوجد زمن).</summary>
    public static string FormatTime(float t)
    {
        if (t <= 0f) return "--:--.---";

        int ms = Mathf.RoundToInt(t * 1000f);
        int minutes = ms / 60000; ms -= minutes * 60000;
        int seconds = ms / 1000;  ms -= seconds * 1000;
        return $"{minutes:00}:{seconds:00}.{ms:000}";
    }
}
