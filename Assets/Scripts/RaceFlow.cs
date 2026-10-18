using UnityEngine;

/// <summary>
/// TurboRacing - المرحلة 4
/// آلة حالات السباق المركزية:
/// Menu (القائمة + كاميرا الحلبة) ← Countdown (عد تنازلي والسيارة مجمّدة)
/// ← Racing (سباق فعلي) ← Paused (وقفة) / Finished (شاشة النتائج).
/// تتحكم بـ: تجميد فيزياء السيارة، إظهار/إخفاء الواجهات، وضع كاميرا القائمة، وTime.timeScale.
/// </summary>
public enum RaceState { Menu, Countdown, Racing, Paused, Finished }

public class RaceFlow : MonoBehaviour
{
    public static RaceFlow Instance { get; private set; }

    /// <summary>عدد اللفات المطلوب لإكمال السباق.</summary>
    public const int LapsPerRace = 3;

    public RaceState State { get; private set; } = RaceState.Menu;

    /// <summary>الوقت المتبقي للعد التنازلي (يُخصم خلال حالة Countdown فقط).</summary>
    public float CountdownRemaining { get; private set; }

    /// <summary>آخر لحظة لإظهار وميض GO! بعد الانطلاق.</summary>
    public float GoFlashUntil { get; private set; }

    int lastBeepSecond; // (v0.7.0) آخر ثانية عُزفت لها نقرة عد تنازلي

    GameObject car;
    Rigidbody2D carRb;
    GameObject gameCanvas;   // أزرار القيادة اللمسية
    CameraFollow camFollow;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        car = GameObject.Find("Car");
        if (car != null) carRb = car.GetComponent<Rigidbody2D>();
        gameCanvas = GameObject.Find("GameCanvas");
        camFollow = Object.FindFirstObjectByType<CameraFollow>();

        EnterMenu(); // الحالة الابتدائية: القائمة الرئيسية
    }

    void Update()
    {
        // العد التنازلي ثم الانطلاق
        if (State == RaceState.Countdown)
        {
            CountdownRemaining -= Time.deltaTime;

            // (v0.7.0) نقرة عند كل ثانية من العد (3..2..1)
            int sec = Mathf.CeilToInt(Mathf.Max(0f, CountdownRemaining));
            if (sec != lastBeepSecond)
            {
                lastBeepSecond = sec;
                if (sec >= 1 && sec <= 3 && SoundManager.Instance != null)
                    SoundManager.Instance.PlayCountBeep();
            }

            if (CountdownRemaining <= 0f)
            {
                State = RaceState.Racing;
                GoFlashUntil = Time.time + 0.9f;
                SetCarFrozen(false);

                // (v0.7.0) نغمة GO + تحويل المحرك من الخمول إلى القيادة
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlayGo();
                    SoundManager.Instance.EngineDrive();
                }
            }
        }

        // ESC / زر الرجوع في أندرويد / P = إيقاف مؤقت واستئناف
        if (State == RaceState.Racing &&
            (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)))
            PauseRace();
        else if (State == RaceState.Paused &&
            (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)))
            ResumeRace();
    }

    // ================= الانتقالات (تستدعيها أزرار الواجهات) =================

    /// <summary>بدء سباق جديد من القائمة: تصفير + عودة للبداية + عد تنازلي.</summary>
    public void StartRace()
    {
        Time.timeScale = 1f;
        ResetCarToStart();
        if (LapManager.Instance != null) LapManager.Instance.ResetRace();

        ShowGameplayUI(menu: false, results: false, pause: false);
        SetGameCanvas(true);

        CountdownRemaining = 3f;
        lastBeepSecond = 3;
        State = RaceState.Countdown;
        SetCarFrozen(true);
        SetCameraMode(follow: true);

        // (v0.7.0) نقرة الثانية الأولى + خمول المحرك أثناء العد
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayCountBeep();
            SoundManager.Instance.EngineIdle();
        }
    }

    /// <summary>إيقاف مؤقت أثناء السباق (تجميد الزمن والفيزياء).</summary>
    public void PauseRace()
    {
        if (State != RaceState.Racing) return;

        State = RaceState.Paused;
        Time.timeScale = 0f; // يجمد الفيزياء ومؤقت اللفة معاً
        if (PauseUI.Instance != null) PauseUI.Instance.SetVisible(true);

        // (v0.7.0) الصوت لا يتأثر بـ timeScale — نسكته صراحة أثناء الوقفة
        if (SoundManager.Instance != null) SoundManager.Instance.EngineSilent();
    }

    /// <summary>استئناف السباق من نقطة التوقف.</summary>
    public void ResumeRace()
    {
        if (State != RaceState.Paused) return;

        Time.timeScale = 1f;
        if (PauseUI.Instance != null) PauseUI.Instance.SetVisible(false);
        State = RaceState.Racing;

        // (v0.7.0) إعادة تشغيل صوت المحرك بعد الاستئناف
        if (SoundManager.Instance != null) SoundManager.Instance.EngineDrive();
    }

    /// <summary>إعادة السباق من الصفر (من الإيقاف المؤقت أو شاشة النتائج).</summary>
    public void RestartRace()
    {
        Time.timeScale = 1f;
        if (PauseUI.Instance != null) PauseUI.Instance.SetVisible(false);
        StartRace();
    }

    /// <summary>العودة إلى القائمة الرئيسية (من الإيقاف المؤقت أو النتائج).</summary>
    public void ToMenu()
    {
        Time.timeScale = 1f;
        EnterMenu();
    }

    /// <summary>يستدعيها LapManager عند إكمال LapsPerRace لفة صحيحة.</summary>
    public void NotifyRaceFinished()
    {
        if (State != RaceState.Racing) return;

        State = RaceState.Finished;
        SetCarFrozen(true);
        SetGameCanvas(false);

        // (v0.7.0) صمت المحرك + فانفار النهاية أو رنة الرقم القياسي الجديد
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.EngineSilent();

            bool newRecord = LapManager.Instance != null &&
                LapManager.Instance.BestLapTime > 0f &&
                (LapManager.Instance.BestAtRaceStart <= 0f ||
                 LapManager.Instance.BestLapTime < LapManager.Instance.BestAtRaceStart);

            if (newRecord) SoundManager.Instance.PlayRecord();
            else SoundManager.Instance.PlayFinish();
        }

        if (ResultsUI.Instance != null) ResultsUI.Instance.Show();
    }

    // ================= الحالات =================

    void EnterMenu()
    {
        State = RaceState.Menu;
        Time.timeScale = 1f;

        ResetCarToStart();
        if (LapManager.Instance != null) LapManager.Instance.ResetRace();

        SetCarFrozen(true);
        SetGameCanvas(false);
        ShowGameplayUI(menu: true, results: false, pause: false);
        SetCameraMode(follow: false); // كاميرا القائمة: تأطير الحلبة كاملة

        // (v0.7.0) صمت كامل في القائمة الرئيسية
        if (SoundManager.Instance != null) SoundManager.Instance.EngineSilent();
    }

    // ================= مساعدات =================

    void ResetCarToStart()
    {
        if (car == null || TrackBuilder.Instance == null) return;

        // تصفير مدخلات القيادة القديمة (v0.6.0): لا توجيه/فرامل عالقة عند
        // بدء سباق جديد أو العودة للقائمة إذا بقي إصبع اللاعب على زر سابق
        CarController carCtrl = car.GetComponent<CarController>();
        if (carCtrl != null) carCtrl.ResetInput();

        Vector3 pos = TrackBuilder.Instance.StartPosition;
        float rot = TrackBuilder.Instance.StartRotationZ;

        if (carRb != null)
        {
            carRb.position = pos;
            carRb.rotation = rot;
            carRb.linearVelocity = Vector2.zero;
            carRb.angularVelocity = 0f; // (v0.6.8) لا دوران متبقٍ من السباق السابق
        }

        car.transform.position = pos;
        car.transform.rotation = Quaternion.Euler(0f, 0f, rot);
    }

    void SetCarFrozen(bool frozen)
    {
        if (carRb != null) carRb.simulated = !frozen;
    }

    void SetGameCanvas(bool on)
    {
        if (gameCanvas != null) gameCanvas.SetActive(on);
    }

    void SetCameraMode(bool follow)
    {
        if (camFollow != null) camFollow.SetOverviewMode(!follow);
    }

    void ShowGameplayUI(bool menu, bool results, bool pause)
    {
        if (MenuUI.Instance != null) MenuUI.Instance.SetVisible(menu);
        if (ResultsUI.Instance != null) ResultsUI.Instance.SetVisible(results);
        if (PauseUI.Instance != null) PauseUI.Instance.SetVisible(pause);
    }
}
