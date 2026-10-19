using UnityEngine;

/// <summary>
/// TurboRacing - v0.8.0 (مطوّر v1.2.0)
/// تخطيط أزرار القيادة القابل للتخصيص (يستدعى من GameSettings.ApplyButtons):
/// - الحجم القياسي: زرا توجيه مجمّعان أسفل اليسار + البنزين (الأكبر) أسفل اليمين والفرامل على يساره
/// - SwapButtons: تبديل الجانبين للاعبين اليساريين (توجيه يمين + بنزين/فرامل يسار)
/// - BtnScale: تكبير/تصغير الأزرار (عبر localScale — كل المحتوى الداخلي يتبعها)
/// - BtnOpacity: شفافية الأزرار (عبر CarController.ApplyButtonOpacity)
/// ملاحظة: المراجع تُخزَّن عند أول تطبيق ناجح (الأزرار نشطة وقت الإقلاع) حتى
/// يعمل التطبيق لاحقاً حتى عندما يكون كانفاس اللعب معطلاً في القائمة الرئيسية
/// (GameObject.Find لا يرى الكائنات غير النشطة).
/// </summary>
public static class DriveButtonsLayout
{
    // القياسات القياسية (بوحدات كانفاس 1920×1080) — مطابقة لإنشاء GameBootstrap
    const float SteerX1 = 250f;
    const float SteerX2 = 580f;
    const float SteerAnchorX = 0f;   // يسار
    const float GasX = 250f;         // من الحافة اليمنى (v1.2.0)
    const float BrakeX = 640f;       // من الحافة اليمنى، على يسار البنزين
    const float PedalAnchorX = 1f;   // يمين
    const float Y = 240f;

    static RectTransform cLeft, cRight, cBrake, cGas;

    /// <summary>
    /// تطبيق التخطيط من الإعدادات الحالية.
    /// car = مرجع اختياري (يُمرَّر من CarController.Start لتفادي بحث إضافي).
    /// </summary>
    public static void Apply(CarController car)
    {
        GameSettings.EnsureLoaded();

        CacheButtons();
        if (cLeft == null || cRight == null || cBrake == null || cGas == null) return;

        bool swap = GameSettings.SwapButtons;

        // زرا التوجيه: يسار الشاشة افتراضياً، يمينها عند التبديل
        PlaceSteer(cLeft, swap, 0);
        PlaceSteer(cRight, swap, 1);

        // البنزين (الأكبر، أقرب للحافة) والفرامل (على يساره):
        // يمين الشاشة افتراضياً، يسارها عند التبديل
        float pax = swap ? 0f : PedalAnchorX;
        cGas.anchoredPosition   = new Vector2(swap ?  GasX : -GasX, Y);
        cBrake.anchoredPosition = new Vector2(swap ? BrakeX : -BrakeX, Y);
        cGas.anchorMin = new Vector2(pax, 0f);
        cGas.anchorMax = new Vector2(pax, 0f);
        cBrake.anchorMin = new Vector2(pax, 0f);
        cBrake.anchorMax = new Vector2(pax, 0f);

        // الحجم
        float s = Mathf.Clamp(GameSettings.BtnScale, 0.5f, 2f);
        Vector3 scl = new Vector3(s, s, 1f);
        cLeft.localScale = scl;
        cRight.localScale = scl;
        cBrake.localScale = scl;
        cGas.localScale = scl;

        // الشفافية (تحتاج سيارة لتعديل ألوان الأساس المخزنة)
        if (car == null)
        {
            GameObject carGO = GameObject.Find("Car");
            if (carGO != null) car = carGO.GetComponent<CarController>();
        }
        if (car != null)
            car.ApplyButtonOpacity(Mathf.Clamp(GameSettings.BtnOpacity, 0.2f, 2f));
    }

    static void PlaceSteer(RectTransform rt, bool swap, int index)
    {
        float x = index == 0 ? SteerX1 : SteerX2;
        float px = swap ? -x : x;
        float ax = swap ? 1f : SteerAnchorX;

        rt.anchorMin = new Vector2(ax, 0f);
        rt.anchorMax = new Vector2(ax, 0f);
        rt.anchoredPosition = new Vector2(px, Y);
    }

    static void CacheButtons()
    {
        if (cLeft == null)  cLeft  = FindBtnRT("BtnLeft");
        if (cRight == null) cRight = FindBtnRT("BtnRight");
        if (cBrake == null) cBrake = FindBtnRT("BtnBrake");
        if (cGas == null)   cGas   = FindBtnRT("BtnGas");
    }

    static RectTransform FindBtnRT(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<RectTransform>() : null;
    }
}
