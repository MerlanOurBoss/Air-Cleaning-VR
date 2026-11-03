using System;
using UnityEngine;

/// <summary>
/// Качество очистки газа: зависит от жидкости, её расхода на единицу газа и мощности насоса.
/// На входе в колонну дым всегда тёмный, а на выходе светлеет и редеет тем сильнее, чем лучше очистка.
/// Коэффициенты учебные, это не расчёт из MathModuleForEmul; их нужно сверять с методикой по установке.
/// </summary>
public class EmulsifierGasCleaning : MonoBehaviour
{
    [SerializeField] private EmulsifierSettings settings;
    [SerializeField] private EmulsifierCalculation calculation;
    [SerializeField] private EmulsifierFault fault;

    [Tooltip("Дым у входа газа в колонну.")]
    [SerializeField] private ParticleSystem[] inletSmokes;
    [Tooltip("Дым в середине колонны: цвет на полпути между входом и выходом.")]
    [SerializeField] private ParticleSystem[] middleSmokes;
    [Tooltip("Дым у выхода газа из колонны.")]
    [SerializeField] private ParticleSystem[] outletSmokes;

    [SerializeField] private Color dirtyColor = new Color(0.2f, 0.17f, 0.14f, 0.7f);
    [SerializeField] private Color cleanColor = new Color(0.92f, 0.95f, 1f, 0.12f);
    [Tooltip("Скорость, с которой дым меняет цвет при смене настроек.")]
    [SerializeField] private float colorSpeed = 0.5f;

    [Header("Очистка жидкостью, доля")]
    [SerializeField] private float waterCleaning = 0.65f;
    [SerializeField] private float sodaCleaning = 0.85f;
    [SerializeField] private float causticSodaCleaning = 0.95f;

    [Header("Расход жидкости на газ, л/м³")]
    [SerializeField] private float poorRatio = 0.5f;
    [SerializeField] private float goodRatio = 5f;
    [Tooltip("Доля очистки при плохом соотношении.")]
    [SerializeField] private float poorRatioFactor = 0.3f;

    // Уровни насоса 1–4: слабый насос хуже распыляет жидкость.
    private static readonly float[] PumpFactor = { 0.85f, 0.95f, 1f, 1f };

    /// <summary>Доля очищенного газа 0–1 при текущих настройках и подаче воды.</summary>
    public float Efficiency { get; private set; }
    public int EfficiencyPercent => Mathf.RoundToInt(Efficiency * 100f);

    public event Action Changed;

    private float _shown;
    private int _lastPercent = -1;

    private void Start()
    {
        Efficiency = Calculate();
        _shown = Efficiency;
        Paint(inletSmokes, 0f);
        Apply();
    }

    private void Update()
    {
        Efficiency = Calculate();
        if (EfficiencyPercent != _lastPercent)
        {
            _lastPercent = EfficiencyPercent;
            Changed?.Invoke();
        }

        if (Mathf.Approximately(_shown, Efficiency)) return;
        _shown = Mathf.MoveTowards(_shown, Efficiency, colorSpeed * Time.deltaTime);
        Apply();
    }

    private float Calculate()
    {
        float liquid;
        switch (settings.Fluid)
        {
            case EmulsifierSettings.FluidType.CausticSoda: liquid = causticSodaCleaning; break;
            case EmulsifierSettings.FluidType.Soda: liquid = sodaCleaning; break;
            default: liquid = waterCleaning; break;
        }

        // м³/с на м³/с = м³/м³, в л/м³ — умножить на 1000.
        float ratio = calculation.WaterFlow / Mathf.Max(calculation.GasFlow, 0.01f) * 1000f;
        float ratioFactor = Mathf.Lerp(poorRatioFactor, 1f, Mathf.InverseLerp(poorRatio, goodRatio, ratio));

        return liquid * ratioFactor * PumpFactor[settings.PumpLevel - 1] * fault.WaterShare;
    }

    private void Apply()
    {
        Paint(middleSmokes, _shown * 0.5f);
        Paint(outletSmokes, _shown);
    }

    private void Paint(ParticleSystem[] smokes, float cleanliness)
    {
        var color = Color.Lerp(dirtyColor, cleanColor, cleanliness);
        foreach (var smoke in smokes)
        {
            var main = smoke.main;
            main.startColor = color;
        }
    }
}
