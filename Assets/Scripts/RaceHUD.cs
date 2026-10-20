using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - المرحلة 4 (تطوير واجهة المرحلة 3)
/// HUD السباق: عدد اللفة الحالية من الإجمالي + زمن اللفة + الأفضل/الأخير
/// + عد تنازلي كبير وسط الشاشة + زر إيقاف مؤقت + شارة الإصدار.
/// يظهر فقط أثناء (Countdown/Racing/Paused) ويُدار من RaceFlow عبر فحص الحالة.
/// </summary>
public class RaceHUD : MonoBehaviour
{
    public static RaceHUD Instance { get; private set; }

    GameObject canvasGO;
    Text lapText;
    Text timeText;
    Text bestText;
    Text lastText;
    Text posText;
    Text countdownText;
    bool hudShown;

    // (v2.1.0) طبقة تشخيصية + تنبيه الإنقاذ
    Text dbgText;
    Text toastText;
    float dbgNext;
    float toastUntil;
    Transform carTr;
    CarController carCtrl;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        BuildCanvas();
        canvasGO.SetActive(false); // RaceFlow سيظهره عند بدء السباق
    }

    void BuildCanvas()
    {
        // ترتيب 10: فوق الحلبة وتحت لوحات القوائم
        canvasGO = UiKit.NewCanvas("HUDCanvas", 10);

        lapText = UiKit.MakeText(canvasGO.transform, "LapText",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -30f),
            new Vector2(500f, 90f), 64, TextAnchor.UpperLeft, Color.white, FontStyle.Bold);

        // (v1.4.0) مركز اللاعب بين المنافسين P1/P5
        posText = UiKit.MakeText(canvasGO.transform, "PosText",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -128f),
            new Vector2(400f, 64f), 46, TextAnchor.UpperLeft, UiKit.Amber, FontStyle.Bold);

        timeText = UiKit.MakeText(canvasGO.transform, "TimeText",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f),
            new Vector2(700f, 110f), 84, TextAnchor.UpperCenter, Color.white, FontStyle.Bold);

        // إزاحة لليسار لتفادي زر الإيقاف أعلى اليمين
        bestText = UiKit.MakeText(canvasGO.transform, "BestText",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150f, -30f),
            new Vector2(700f, 60f), 44, TextAnchor.UpperRight, Color.white);

        lastText = UiKit.MakeText(canvasGO.transform, "LastText",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150f, -96f),
            new Vector2(700f, 60f), 44, TextAnchor.UpperRight, Color.white);

        countdownText = UiKit.MakeText(canvasGO.transform, "CountdownText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f),
            new Vector2(800f, 280f), 170, TextAnchor.MiddleCenter, UiKit.Amber, FontStyle.Bold);
        countdownText.text = "";

        // (v2.1.0) طبقة تشخيصية صغيرة أعلى اليسار — إحداثيات السيارة وسطحها
        // ومسافتها عن خط السباق: كل لقطة شاشة من المستخدم تصبح تقريراً دقيقاً
        dbgText = UiKit.MakeText(canvasGO.transform, "DbgText",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -200f),
            new Vector2(640f, 130f), 30, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.85f));
        dbgText.text = "";

        // (v2.1.0) تنبيه الإنقاذ (توست) أسفل المؤقت
        toastText = UiKit.MakeText(canvasGO.transform, "ToastText",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f),
            new Vector2(1000f, 70f), 44, TextAnchor.UpperCenter, UiKit.Amber, FontStyle.Bold);
        toastText.text = "";

        UiKit.MakeText(canvasGO.transform, "VersionText",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, -12f),
            new Vector2(300f, 40f), 26, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.55f))
            .text = "v" + GameVersion.Number;

        // زر الإيقاف المؤقت (v0.6.6) - أيقونة حقيقية (شريطان أبيضان) مرسومة بالصور
        // أعلى يمين الشاشة — مستقلة تماماً عن الخطوط فتظهر دائماً على كل الأجهزة
        UiKit.MakePauseButton(canvasGO.transform, "BtnPause",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(100f, 100f),
            () => { if (RaceFlow.Instance != null) RaceFlow.Instance.PauseRace(); });
    }

    void Update()
    {
        RaceFlow rf = RaceFlow.Instance;
        if (rf == null) return;

        // إدارة الظهور حسب حالة السباق (فحص رخيص مع كاش)
        bool show = rf.State == RaceState.Racing ||
                    rf.State == RaceState.Paused ||
                    rf.State == RaceState.Countdown;
        if (show != hudShown)
        {
            hudShown = show;
            if (canvasGO != null) canvasGO.SetActive(show);
        }
        if (!show) return;

        // العد التنازلي ووميض GO!
        if (rf.State == RaceState.Countdown)
            countdownText.text = Mathf.CeilToInt(Mathf.Max(0f, rf.CountdownRemaining)).ToString();
        else if (rf.State == RaceState.Racing && Time.time < rf.GoFlashUntil)
            countdownText.text = "GO!";
        else
            countdownText.text = "";

        LapManager lm = LapManager.Instance;
        if (lm == null) return;

        lapText.text = "LAP " + Mathf.Min(lm.LapsCompleted + 1, RaceFlow.LapsPerRace) +
                       "/" + RaceFlow.LapsPerRace;

        // (v1.4.0) المركز الحي بين الشبكة (لاعب + منافسون)
        if (AiManager.Instance != null && AiManager.Instance.FieldSize > 1)
            posText.text = "P" + AiManager.Instance.PlayerPosition + "/" + AiManager.Instance.FieldSize;
        else
            posText.text = "";

        timeText.text = UiKit.FormatTime(lm.CurrentLapTime);
        bestText.text = "BEST " + UiKit.FormatTime(lm.BestLapTime);
        lastText.text = lm.LastLapTime > 0f ? "LAST " + UiKit.FormatTime(lm.LastLapTime) : "";

        UpdateDebug();
        if (toastText != null && Time.time > toastUntil && toastText.text.Length > 0)
            toastText.text = "";
    }

    /// <summary>(v2.1.0) توست رسالة قصيرة (نتائج الإنقاذ وأخبار المسار).</summary>
    public void ShowToast(string msg)
    {
        if (toastText == null) return;
        toastText.text = msg;
        toastUntil = Time.time + 2f;
    }

    /// <summary>(v2.1.0) الطبقة التشخيصية: موضع السيارة وسطحها ومسافتها عن
    /// خط السباق — تحويل أي شكوى مستخدم إلى بيانات قابلة للتصحيح فوراً.</summary>
    void UpdateDebug()
    {
        if (dbgText == null || Time.unscaledTime < dbgNext) return;
        dbgNext = Time.unscaledTime + 0.2f;

        if (carTr == null)
        {
            GameObject c = GameObject.Find("Car");
            if (c != null) { carTr = c.transform; carCtrl = c.GetComponent<CarController>(); }
        }
        if (carTr == null) { dbgText.text = ""; return; }

        Vector3 p = carTr.position;
        TrackBuilder tb = TrackBuilder.Instance;
        string surf = tb != null ? tb.SurfaceNameAt(p.x, p.z) : "?";

        float lineD = -1f;
        Vector3[] line = tb != null ? tb.RacingLine : null;
        if (line != null && line.Length > 0)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Length; i++)
            {
                float dx = line[i].x - p.x, dz = line[i].z - p.z;
                float d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            lineD = Mathf.Sqrt(best);
        }

        // (v2.2.0) السطر الثالث: مصدر خط السباق + حالة المدخلات الحية + السرعة
        // — من أي لقطة نعرف فوراً: هل الخط مخبوز؟ هل وصل ضغط البنزين للفيزياء؟
        float spd = carCtrl != null ? carCtrl.SpeedKmh : 0f;
        string inp = carCtrl != null
            ? $"G{(carCtrl.DebugGasHeld ? "+" : "-")} S{(carCtrl.DebugSteer > 0f ? "+" : carCtrl.DebugSteer < 0f ? "-" : "0")} B{(carCtrl.DebugBrakeHeld ? "+" : "-")}"
            : "?";
        string src = TrackBuilder.LineSourceTag;

        dbgText.text = $"X {p.x:0.#}  Y {p.y:0.#}  Z {p.z:0.#}\n" +
                       $"{surf}   line {(lineD >= 0f ? lineD.ToString("0") + "m" : "-")}\n" +
                       $"{src}   {spd:0}km/h  {inp}";
    }
}
