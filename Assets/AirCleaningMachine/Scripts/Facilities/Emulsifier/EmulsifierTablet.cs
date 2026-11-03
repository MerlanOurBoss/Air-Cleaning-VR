using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VR-планшет управления эмульгатором: пуск/стоп, мощность насоса 1–4, тип жидкости, прозрачный корпус.
/// Выбранные кнопки подсвечиваются цветом "selected" из стиля кнопок шаблона.
/// </summary>
public class EmulsifierTablet : MonoBehaviour
{
    [SerializeField] private EmulsifierEffects effects;
    [SerializeField] private EmulsifierSettings settings;
    [SerializeField] private EmulsifierXRay xray;
    [SerializeField] private EmulsifierGasCleaning cleaning;

    [SerializeField] private Button powerButton;
    [SerializeField] private TMP_Text powerLabel;
    [SerializeField] private Button[] pumpButtons;   // уровни 1..4
    [SerializeField] private Button[] fluidButtons;  // Вода, Едкий натрий, Сода
    [SerializeField] private Button xrayButton;
    [SerializeField] private TMP_Text xrayLabel;
    [SerializeField] private TMP_Text statusText;

    [SerializeField] private Color normalColor = new Color(0.18f, 0.18f, 0.18f);
    [SerializeField] private Color activeColor = new Color(0.125f, 0.588f, 0.953f);
    [SerializeField] private Color stopColor = new Color(0.8f, 0.25f, 0.2f);

    private static readonly string[] FluidKeys = { "fluid.water", "fluid.caustic", "fluid.soda" };

    private void Start()
    {
        // Префаб планшета не может ссылаться на объекты сцены — берём эмульгатор из сцены, если не задан.
        if (effects == null) effects = FindAnyObjectByType<EmulsifierEffects>();
        if (settings == null) settings = FindAnyObjectByType<EmulsifierSettings>();
        if (xray == null) xray = FindAnyObjectByType<EmulsifierXRay>();
        if (cleaning == null) cleaning = FindAnyObjectByType<EmulsifierGasCleaning>();

        powerButton.onClick.AddListener(effects.Toggle);
        xrayButton.onClick.AddListener(xray.Toggle);

        for (int i = 0; i < pumpButtons.Length; i++)
        {
            int level = i + 1;
            pumpButtons[i].onClick.AddListener(() => settings.SetPumpLevel(level));
        }

        for (int i = 0; i < fluidButtons.Length; i++)
        {
            int type = i + 1;
            fluidButtons[i].onClick.AddListener(() => settings.SetFluid(type));
        }

        settings.Changed += Refresh;
        effects.StateChanged += Refresh;
        xray.Changed += Refresh;
        cleaning.Changed += Refresh;
        L10n.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        L10n.Changed -= Refresh;
        if (settings != null) settings.Changed -= Refresh;
        if (effects != null) effects.StateChanged -= Refresh;
        if (xray != null) xray.Changed -= Refresh;
        if (cleaning != null) cleaning.Changed -= Refresh;
    }

    private void Refresh()
    {
        bool running = effects.IsPlaying;
        powerLabel.text = L10n.Get(running ? "ctl.stop" : "ctl.start");
        Tint(powerButton, running ? stopColor : activeColor);

        for (int i = 0; i < pumpButtons.Length; i++)
            Tint(pumpButtons[i], i + 1 == settings.PumpLevel ? activeColor : normalColor);

        int fluid = (int)settings.Fluid;
        for (int i = 0; i < fluidButtons.Length; i++)
            Tint(fluidButtons[i], i + 1 == fluid ? activeColor : normalColor);

        xrayLabel.text = L10n.Get(xray.IsOn ? "ctl.xray.off" : "ctl.xray.on");
        Tint(xrayButton, xray.IsOn ? activeColor : normalColor);

        statusText.text = L10n.Get("ctl.status", L10n.Get(running ? "ctl.running" : "ctl.stopped"), settings.PumpLevel, L10n.Get(FluidKeys[fluid - 1]))
                          + (running ? L10n.Get("ctl.status.cleaning", cleaning.EfficiencyPercent) : "")
                          + (effects.PowerLocked ? L10n.Get("ctl.status.locked") : "");
    }

    internal static void Tint(Button button, Color color)
    {
        var colors = button.colors;
        colors.normalColor = color;
        colors.selectedColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
        button.colors = colors;
    }
}
