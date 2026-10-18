using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// TurboRacing - المرحلة 3 (مطوّرة في المرحلة 4)
/// عداد اللفات + المؤقت + الحفظ:
/// - 3 نقاط تفتيش إلزامية (يمين، أعلى، يسار) ثم خط النهاية = لفة صحيحة
///   (منع الغش: قطع الطريق عبر العشب لا يحتسب النقطة لأن نطاق كل بوابة مقيّد بعرض الطريق)
/// - المؤقت يبدأ فعلياً عند أول عبور لخط النهاية باتجاه السباق
/// - أفضل لفة تُحفظ في PlayerPrefs (تبقى بين الجلسات) + إحصاء إجمالي اللفات
/// جديد في المرحلة 4:
/// - السباق = LapsPerRace لفة (3) وعند إكمالها يُبلغ RaceFlow لإظهار النتائج
/// - تسجيل أزمنة كل لفات السباق (LapTimes) لإظهارها في شاشة النتائج
/// - ResetRace لإعادة الضبط الكامل عند البدء/الإعادة + حارس حالة (العد أثناء Racing فقط)
/// </summary>
public class LapManager : MonoBehaviour
{
    public static LapManager Instance { get; private set; }

    const string BestKey = "TR_BestLapMs";
    const string TotalKey = "TR_TotalLaps";

    Rigidbody2D carRb;
    TrackBuilder track;

    int progress;            // فهرس البوابة التالية (0..2 = نقاط التفتيش)
    bool timing;             // هل بدأ التوقيت؟
    float lapStartTime;
    float prevX, prevY;

    public int LapsCompleted { get; private set; }
    public float CurrentLapTime => timing ? Time.time - lapStartTime : 0f;
    public float LastLapTime { get; private set; }
    public float BestLapTime { get; private set; }
    public int TotalLapsAllTime { get; private set; }
    public bool Timing => timing;

    // --- جديد في المرحلة 4 ---
    /// <summary>أزمنة لفات السباق الحالي (تُصفّر مع ResetRace).</summary>
    public List<float> LapTimes { get; } = new List<float>();

    /// <summary>قيمة أفضل لفة قبل بدء السباق الحالي (لكشف الرقم القياسي الجديد في النتائج).</summary>
    public float BestAtRaceStart { get; private set; }

    /// <summary>يُطلق عند إكمال LapsPerRace لفة صحيحة (خطاف للصوت/المؤثرات لاحقاً).</summary>
    public event System.Action RaceFinished;

    void Awake()
    {
        Instance = this;

        // استرجاع الحفظ (أفضل زمن بالمللي ثانية + إجمالي اللفات)
        int bestMs = PlayerPrefs.GetInt(BestKey, 0);
        BestLapTime = bestMs > 0 ? bestMs / 1000f : 0f;
        TotalLapsAllTime = PlayerPrefs.GetInt(TotalKey, 0);
    }

    void Start()
    {
        GameObject car = GameObject.Find("Car");
        if (car == null)
        {
            Debug.LogWarning("[LapManager] لم أجد السيارة - تعطيل العد");
            enabled = false;
            return;
        }

        carRb = car.GetComponent<Rigidbody2D>();
        track = TrackBuilder.Instance;
        prevX = carRb.position.x;
        prevY = carRb.position.y;
    }

    /// <summary>
    /// إعادة ضبط كامل لعدّاد السباق (المرحلة 4).
    /// استدعِها بعد إعادة تموضع السيارة لأنها تحاذي prev مع الموضع الجديد
    /// (منع عبور وهمي للبوابات بعد الانتقال الفوري).
    /// </summary>
    public void ResetRace()
    {
        progress = 0;
        timing = false;
        lapStartTime = 0f;
        LapsCompleted = 0;
        LastLapTime = 0f;
        LapTimes.Clear();
        BestAtRaceStart = BestLapTime;

        if (carRb != null)
        {
            prevX = carRb.position.x;
            prevY = carRb.position.y;
        }
    }

    void FixedUpdate()
    {
        if (carRb == null || track == null || track.LapGates == null) return;

        Vector2 p = carRb.position;

        // المرحلة 4: العد يعمل أثناء السباق الفعلي فقط
        if (RaceFlow.Instance != null && RaceFlow.Instance.State != RaceState.Racing)
        {
            prevX = p.x;
            prevY = p.y;
            return;
        }

        // 1) بوابة نقطة التفتيش التالية (إن وجدت)
        if (progress < 3)
            CheckGate(track.LapGates[progress], p, onCheckpoint: true);

        // 2) خط النهاية يُفحص دائماً (انطلاق التوقيت أو إتمام لفة)
        CheckGate(track.LapGates[3], p, onCheckpoint: false);

        prevX = p.x;
        prevY = p.y;
    }

    void CheckGate(TrackBuilder.LapGate gate, Vector2 p, bool onCheckpoint)
    {
        float prevA = gate.axis == 0 ? prevX : prevY;
        float curA  = gate.axis == 0 ? p.x : p.y;
        float other = gate.axis == 0 ? p.y : p.x;

        // عبور المستوى بالاتجاه المطلوب فقط
        bool crossed = gate.dir > 0
            ? (prevA < gate.plane && curA >= gate.plane)
            : (prevA > gate.plane && curA <= gate.plane);

        if (!crossed) return;
        if (other < gate.spanMin || other > gate.spanMax) return; // خارج عرض الطريق = لا احتساب

        if (onCheckpoint)
        {
            progress++;
            return;
        }

        // عبور خط النهاية
        if (progress >= 3)
        {
            // لفة مكتملة
            progress = 0;

            float lapTime = Time.time - lapStartTime;
            lapStartTime = Time.time;
            LastLapTime = lapTime;
            LapsCompleted++;
            LapTimes.Add(lapTime);

            if (BestLapTime <= 0f || lapTime < BestLapTime)
            {
                BestLapTime = lapTime;
                PlayerPrefs.SetInt(BestKey, Mathf.RoundToInt(lapTime * 1000f));
            }

            TotalLapsAllTime++;
            PlayerPrefs.SetInt(TotalKey, TotalLapsAllTime);
            PlayerPrefs.Save(); // حفظ فوري (مقاوم لإغلاق التطبيق)

            Debug.Log($"[LapManager] لفة {LapsCompleted}: {lapTime:F3} ث | الأفضل: {BestLapTime:F3} ث");

            // المرحلة 4: إكمال عدد لفات السباق → شاشة النتائج
            if (LapsCompleted >= RaceFlow.LapsPerRace && RaceFlow.Instance != null)
            {
                RaceFlow.Instance.NotifyRaceFinished();
                RaceFinished?.Invoke();
            }
        }
        else if (!timing)
        {
            // أول عبور لخط البداية: انطلاق توقيت اللفة الأولى
            timing = true;
            lapStartTime = Time.time;
        }
    }
}
