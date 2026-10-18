using UnityEngine;

/// <summary>
/// TurboRacing - المرحلة 3
/// عداد اللفات + المؤقت + الحفظ:
/// - 3 نقاط تفتيش إلزامية (يمين، أعلى، يسار) ثم خط النهاية = لفة صحيحة
///   (منع الغش: قطع الطريق عبر العشب لا يحتسب النقطة لأن نطاق كل بوابة مقيّد بعرض الطريق)
/// - المؤقت يبدأ فعلياً عند أول عبور لخط النهاية باتجاه السباق
/// - أفضل لفة تُحفظ في PlayerPrefs (تبقى بين الجلسات) + إحصاء إجمالي اللفات
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

    void FixedUpdate()
    {
        if (carRb == null || track == null || track.LapGates == null) return;

        Vector2 p = carRb.position;

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

            if (BestLapTime <= 0f || lapTime < BestLapTime)
            {
                BestLapTime = lapTime;
                PlayerPrefs.SetInt(BestKey, Mathf.RoundToInt(lapTime * 1000f));
            }

            TotalLapsAllTime++;
            PlayerPrefs.SetInt(TotalKey, TotalLapsAllTime);
            PlayerPrefs.Save(); // حفظ فوري (مقاوم لإغلاق التطبيق)

            Debug.Log($"[LapManager] لفة {LapsCompleted}: {lapTime:F3} ث | الأفضل: {BestLapTime:F3} ث");
        }
        else if (!timing)
        {
            // أول عبور لخط البداية: انطلاق توقيت اللفة الأولى
            timing = true;
            lapStartTime = Time.time;
        }
    }
}
