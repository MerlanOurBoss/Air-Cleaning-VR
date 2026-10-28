using System;
using UnityEngine;

/// <summary>
/// Расчёт эмульгатора — формулы MathModuleForEmul из AirCleaningMachine без полей ввода и переводов:
/// входы задаются числами, тип жидкости берётся из EmulsifierSettings.
/// </summary>
public class EmulsifierCalculation : MonoBehaviour
{
    // Варианты входов — как в выпадающих списках оригинала.
    public static readonly float[] MainGasFlowOptions = { 100000f, 150000f, 200000f, 250000f, 400000f, 500000f }; // м³/ч
    public static readonly string[] MainGasFlowNotes = { "", "ТЭЦ Павлодар", "", "ТЭЦ Алматы", "", "" };
    public static readonly float[] GasFlowOptions = { 10f, 12f, 14f };        // м³/с
    public static readonly float[] WaterFlowOptions = { 0.01f, 0.05f, 0.1f }; // м³/с
    public static readonly float[] TemperatureOptions = { 5f, 15f, 25f };     // °C

    [SerializeField] private EmulsifierSettings settings;
    [SerializeField] private ParticleSystem[] smokes;

    [SerializeField] private float mainGasFlow = 100000f;
    [SerializeField] private float gasFlow = 14f;
    [SerializeField] private float waterFlow = 0.1f;
    [SerializeField] private float temperature = 15f;

    private const float EmpiricalA = 0.5f;
    private const float EmpiricalB = 0.8f;
    private const float DropletDiameter = 1f;

    public float MainGasFlow => mainGasFlow;
    public float GasFlow => gasFlow;
    public float WaterFlow => waterFlow;
    public float Temperature => temperature;

    public float GasSpeed { get; private set; }
    public float WaterSpeed { get; private set; }
    public float GasMassFlow { get; private set; }
    public float WaterMassFlow { get; private set; }
    public float MassTransfer { get; private set; }
    public float LiquidConsumption { get; private set; }
    public float Diameter { get; private set; }
    public double Height { get; private set; }
    public double ConsumablesCost { get; private set; }

    public event Action Changed;

    // Копии исходных ключей: объект Gradient модуль переиспользует, и ссылка на него "плыла" бы вместе с правками.
    private GradientColorKey[][] _baseColorKeys;
    private GradientAlphaKey[][] _baseAlphaKeys;
    private GradientMode[] _baseModes;

    private void Awake()
    {
        _baseColorKeys = new GradientColorKey[smokes.Length][];
        _baseAlphaKeys = new GradientAlphaKey[smokes.Length][];
        _baseModes = new GradientMode[smokes.Length];
        for (int i = 0; i < smokes.Length; i++)
        {
            if (smokes[i] == null) continue;
            var gradient = smokes[i].colorOverLifetime.color.gradient;
            _baseColorKeys[i] = gradient.colorKeys;
            _baseAlphaKeys[i] = gradient.alphaKeys;
            _baseModes[i] = gradient.mode;
        }
    }

    private void OnEnable()
    {
        if (settings != null) settings.Changed += Recalculate;
        Recalculate();
    }

    private void OnDisable()
    {
        if (settings != null) settings.Changed -= Recalculate;
    }

    public void SetMainGasFlow(float value) { mainGasFlow = value; Recalculate(); }
    public void SetGasFlow(float value) { gasFlow = value; Recalculate(); }
    public void SetWaterFlow(float value) { waterFlow = value; Recalculate(); }
    // В формулах оригинала температура не участвует — хранится для отображения.
    public void SetTemperature(float value) { temperature = value; Recalculate(); }

    private void Recalculate()
    {
        var fluid = settings != null ? settings.Fluid : EmulsifierSettings.FluidType.Water;
        double q = mainGasFlow;

        float consumption = (float)q / 3600;
        LiquidConsumption = (0.22f * consumption) / 1000;

        Diameter = (float)Math.Ceiling(Math.Sqrt((4.0 * q) / Math.PI / 3600.0 / 2.9));
        Height = Math.Ceiling(((0.8 + 18 * 0.15 + 0.35 * Diameter + 0.6) / 5.0) * 10.0) / 10.0 * 5.0;

        float crossSection = 3.14159f * Mathf.Pow(Diameter, 2);
        GasSpeed = crossSection > 0f ? 4 * gasFlow / crossSection : 0f;
        WaterSpeed = crossSection > 0f ? 4 * waterFlow / crossSection : 0f;
        GasMassFlow = GasSpeed * crossSection / 4;
        WaterMassFlow = WaterSpeed * crossSection / 4;

        float density, viscosity, reagentCost;
        switch (fluid)
        {
            case EmulsifierSettings.FluidType.CausticSoda: density = 1100f; viscosity = 0.0012f; reagentCost = 200000f; break;
            case EmulsifierSettings.FluidType.Soda: density = 1050f; viscosity = 0.0013f; reagentCost = 125000f; break;
            default: density = 1000f; viscosity = 0.001f; reagentCost = 1f; break;
        }

        float reynolds = density * WaterSpeed * DropletDiameter / viscosity;
        MassTransfer = EmpiricalA * Mathf.Pow(reynolds / DropletDiameter, EmpiricalB);

        double electro = 0.12 * (LiquidConsumption * 3600);
        double waterConsumables = 0.005 * (q / 1000);
        double dryReagentCount = (10 / 100.0) * (1100 / 1000.0) * (q / 1000.0) / 0.95 / 1000;
        double dryReagent = dryReagentCount * reagentCost;
        double other = (8090 + 0.036 * q) / 365 / 24;
        ConsumablesCost = electro * 38.85 + waterConsumables * 59.84 + dryReagent + other;

        ApplySmokeDensity();
        Changed?.Invoke();
    }

    // Как UpdateSmokeAlphaForGasFlow в оригинале: 10 м³/с → 0.3, 12 → 0.7, иначе 1.
    // Оригинал заменял кривую на константу; здесь масштабируем её, чтобы дым не терял плавное появление.
    private void ApplySmokeDensity()
    {
        if (_baseAlphaKeys == null) return;

        float alpha = Mathf.Approximately(gasFlow, 10f) ? 0.3f
                    : Mathf.Approximately(gasFlow, 12f) ? 0.7f
                    : 1f;

        for (int i = 0; i < smokes.Length; i++)
        {
            if (smokes[i] == null || _baseAlphaKeys[i] == null) continue;

            var alphaKeys = (GradientAlphaKey[])_baseAlphaKeys[i].Clone();
            for (int k = 0; k < alphaKeys.Length; k++) alphaKeys[k].alpha *= alpha;

            var gradient = new Gradient { mode = _baseModes[i] };
            gradient.SetKeys(_baseColorKeys[i], alphaKeys);

            var col = smokes[i].colorOverLifetime;
            col.color = new ParticleSystem.MinMaxGradient(gradient);
        }
    }
}
