using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - المرحلة 4
/// شاشة النتائج عند إكمال السباق: قائمة أزمنة اللفات (أفضل لفة بلون مميز)
/// + الزمن الإجمالي + تنبيه "رقم قياسي جديد" + زرا إعادة السباق والقائمة الرئيسية.
/// </summary>
public class ResultsUI : MonoBehaviour
{
    public static ResultsUI Instance { get; private set; }

    GameObject root;
    Text lapsText;
    Text summaryText;
    Text recordText;

    void Awake()
    {
        Instance = this;
        Build();
        SetVisible(false);
    }

    public void SetVisible(bool on)
    {
        if (root != null) root.SetActive(on);
    }

    void Build()
    {
        GameObject canvas = UiKit.NewCanvas("ResultsCanvas", 30);
        root = UiKit.FullRect(canvas.transform, "Root", UiKit.OverlayDark, raycast: true);

        UiKit.MakeText(root, "Title",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1500f, 130f),
            96, TextAnchor.MiddleCenter, UiKit.Amber, FontStyle.Bold).text = "RACE COMPLETE!";

        recordText = UiKit.MakeText(root, "NewRecord",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(1200f, 70f),
            52, TextAnchor.MiddleCenter, new Color(1f, 0.42f, 0.15f), FontStyle.Bold);
        recordText.text = "NEW RECORD!";
        recordText.gameObject.SetActive(false);

        lapsText = UiKit.MakeText(root, "Laps",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(900f, 380f),
            46, TextAnchor.UpperCenter, Color.white);
        lapsText.supportRichText = true;

        summaryText = UiKit.MakeText(root, "Summary",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(1000f, 140f),
            50, TextAnchor.UpperCenter, UiKit.Amber);

        UiKit.MakeButton(root, "BtnAgain", "RACE AGAIN",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 210f), new Vector2(560f, 110f),
            UiKit.Amber, UiKit.BtnAmberText, 52,
            () => { if (RaceFlow.Instance != null) RaceFlow.Instance.RestartRace(); });

        UiKit.MakeButton(root, "BtnMenu", "MAIN MENU",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(560f, 96f),
            UiKit.BtnDark, UiKit.BtnLightText, 42,
            () => { if (RaceFlow.Instance != null) RaceFlow.Instance.ToMenu(); });
    }

    /// <summary>يعبّئ النتائج من LapManager ثم يعرض الشاشة.</summary>
    public void Show()
    {
        LapManager lm = LapManager.Instance;
        if (lm == null) return;

        // أفضل لفة داخل هذا السباق (للتلوين وكشف الرقم القياسي)
        float raceBest = float.MaxValue;
        foreach (float t in lm.LapTimes)
            if (t < raceBest) raceBest = t;
        if (lm.LapTimes.Count == 0) raceBest = 0f;

        // قائمة اللفات - الأفضل بين قوسين وبلون مميز
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lm.LapTimes.Count; i++)
        {
            float t = lm.LapTimes[i];
            bool isBest = t > 0f && Mathf.Approximately(t, raceBest);
            string line = "LAP " + (i + 1) + "      " + UiKit.FormatTime(t);
            sb.AppendLine(isBest ? "<color=#FFC24D>" + line + "  (BEST)</color>" : line);
        }
        lapsText.text = sb.ToString();

        // الإجمالي = مجموع اللفات، وأفضل لفة على الإطلاق
        float total = 0f;
        foreach (float t in lm.LapTimes) total += t;
        summaryText.text = "TOTAL  " + UiKit.FormatTime(total) +
                           "\nBEST LAP  " + UiKit.FormatTime(lm.BestLapTime);

        // رقم قياسي: أفضل لفة اليوم أفضل من المحفوظ قبل السباق (أو لا يوجد حفظ سابق)
        bool newRecord = raceBest > 0f &&
                         (lm.BestAtRaceStart <= 0f || raceBest < lm.BestAtRaceStart);
        recordText.gameObject.SetActive(newRecord);

        SetVisible(true);
    }
}
