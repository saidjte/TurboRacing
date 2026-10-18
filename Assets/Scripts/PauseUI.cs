using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - المرحلة 4
/// لوحة الإيقاف المؤقت: استئناف / إعادة السباق / القائمة الرئيسية.
/// تُفتح من زر II في الـ HUD أو ESC أو زر الرجوع في أندرويد.
/// </summary>
public class PauseUI : MonoBehaviour
{
    public static PauseUI Instance { get; private set; }

    RectTransform root;      // الجذر لوحة كاملة (FullRect يعيد RectTransform)

    void Awake()
    {
        Instance = this;
        Build();
        SetVisible(false);
    }

    public void SetVisible(bool on)
    {
        if (root != null) root.gameObject.SetActive(on);
    }

    void Build()
    {
        GameObject canvas = UiKit.NewCanvas("PauseCanvas", 20);
        root = UiKit.FullRect(canvas.transform, "Root", UiKit.OverlaySoft, raycast: true);

        UiKit.MakeText(root, "Title",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1200f, 140f),
            100, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold).text = "PAUSED";

        UiKit.MakeButton(root, "BtnResume", "RESUME",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(520f, 110f),
            UiKit.Amber, UiKit.BtnAmberText, 52,
            () => { if (RaceFlow.Instance != null) RaceFlow.Instance.ResumeRace(); });

        UiKit.MakeButton(root, "BtnRestart", "RESTART RACE",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(520f, 96f),
            UiKit.BtnDark, UiKit.BtnLightText, 42,
            () => { if (RaceFlow.Instance != null) RaceFlow.Instance.RestartRace(); });

        UiKit.MakeButton(root, "BtnMenu", "MAIN MENU",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -305f), new Vector2(520f, 96f),
            UiKit.BtnDark, UiKit.BtnLightText, 42,
            () => { if (RaceFlow.Instance != null) RaceFlow.Instance.ToMenu(); });
    }
}
