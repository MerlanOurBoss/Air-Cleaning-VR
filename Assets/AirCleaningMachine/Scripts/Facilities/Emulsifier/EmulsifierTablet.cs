using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VR-планшет управления эмульгатором: пуск/стоп, мощность насоса 1–4, тип жидкости.
/// Выбранные кнопки подсвечиваются цветом "selected" из стиля кнопок шаблона.
/// </summary>
public class EmulsifierTablet : MonoBehaviour
{
    [SerializeField] private EmulsifierEffects effects;
    [SerializeField] private EmulsifierSettings settings;

    [SerializeField] private Button powerButton;
    [SerializeField] private TMP_Text powerLabel;
    [SerializeField] private Button[] pumpButtons;   // уровни 1..4
    [SerializeField] private Button[] fluidButtons;  // Вода, Едкий натрий, Сода
    [SerializeField] private TMP_Text statusText;

    [SerializeField] private Color normalColor = new Color(0.18f, 0.18f, 0.18f);
    [SerializeField] private Color activeColor = new Color(0.125f, 0.588f, 0.953f);
    [SerializeField] private Color stopColor = new Color(0.8f, 0.25f, 0.2f);

    private static readonly string[] FluidNames = { "Вода", "Едкий натрий", "Сода" };

    private void Start()
    {
        // Префаб планшета не может ссылаться на объекты сцены — берём эмульгатор из сцены, если не задан.
        if (effects == null) effects = FindAnyObjectByType<EmulsifierEffects>();
        if (settings == null) settings = FindAnyObjectByType<EmulsifierSettings>();

        powerButton.onClick.AddListener(effects.Toggle);

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
        Refresh();
    }

    private void OnDestroy()
    {
        if (settings != null) settings.Changed -= Refresh;
        if (effects != null) effects.StateChanged -= Refresh;
    }

    private void Refresh()
    {
        bool running = effects.IsPlaying;
        powerLabel.text = running ? "Стоп" : "Пуск";
        Tint(powerButton, running ? stopColor : activeColor);

        for (int i = 0; i < pumpButtons.Length; i++)
            Tint(pumpButtons[i], i + 1 == settings.PumpLevel ? activeColor : normalColor);

        int fluid = (int)settings.Fluid;
        for (int i = 0; i < fluidButtons.Length; i++)
            Tint(fluidButtons[i], i + 1 == fluid ? activeColor : normalColor);

        statusText.text = (running ? "Работает" : "Остановлен")
                          + "  ·  насос " + settings.PumpLevel
                          + "  ·  " + FluidNames[fluid - 1];
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
