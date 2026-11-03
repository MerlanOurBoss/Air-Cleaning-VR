using UnityEngine;

/// <summary>
/// Звук установки: вентилятор, насос и вода раскручиваются при пуске и затихают при стопе,
/// контактор щёлкает при переключении. Тон насоса и громкость воды следуют за настройками планшета
/// и уровнем жидкости, тон и громкость вентилятора — за расходом газа.
/// Выполняется после EmulsifierEffects: автозапуск в его Start не должен щёлкать контактором.
/// </summary>
[DefaultExecutionOrder(100)]
public class EmulsifierAudio : MonoBehaviour
{
    [SerializeField] private EmulsifierEffects effects;
    [SerializeField] private EmulsifierSettings settings;
    [SerializeField] private EmulsifierCalculation calculation;
    [SerializeField] private EmulsifierFault fault;

    [SerializeField] private AudioSource fan;
    [SerializeField] private AudioSource pump;
    [SerializeField] private AudioSource water;
    [SerializeField] private AudioSource contactor;

    [Tooltip("Время раскрутки и остановки, с.")]
    [SerializeField] private float spinTime = 2.5f;
    [Tooltip("Тон на долю скорости при остановке: мотор «садится», а не просто затихает.")]
    [SerializeField] private float stoppedPitch = 0.55f;
    [Tooltip("Скорость сглаживания при смене настроек.")]
    [SerializeField] private float smoothing = 3f;

    // Уровни насоса 1–4. В NasosScript уровни 1 и 2 не отличаются по скорости, на слух разница нужна.
    private static readonly float[] PumpPitch = { 0.88f, 1f, 1.12f, 1.25f };
    private static readonly float[] WaterVolume = { 0.7f, 0.8f, 0.9f, 1f };

    private float _fanVolume, _pumpVolume, _waterVolume;
    private float _run;

    private void Start()
    {
        _fanVolume = fan.volume;
        _pumpVolume = pump.volume;
        _waterVolume = water.volume;
        _run = effects.IsPlaying ? 1f : 0f;
        effects.StateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        effects.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged() => contactor.Play();

    private void Update()
    {
        _run = Mathf.MoveTowards(_run, effects.IsPlaying ? 1f : 0f, Time.deltaTime / Mathf.Max(spinTime, 0.01f));
        float run = Mathf.SmoothStep(0f, 1f, _run);

        var mainFlows = EmulsifierCalculation.MainGasFlowOptions;
        float mainFlow = Mathf.InverseLerp(mainFlows[0], mainFlows[mainFlows.Length - 1], calculation.MainGasFlow);
        var gasFlows = EmulsifierCalculation.GasFlowOptions;
        float gasFlow = Mathf.InverseLerp(gasFlows[0], gasFlows[gasFlows.Length - 1], calculation.GasFlow);
        var waterFlows = EmulsifierCalculation.WaterFlowOptions;
        float waterFlow = Mathf.InverseLerp(waterFlows[0], waterFlows[waterFlows.Length - 1], calculation.WaterFlow);
        int level = settings.PumpLevel - 1;

        Drive(fan, _fanVolume * Mathf.Lerp(0.7f, 1f, mainFlow) * run, Mathf.Lerp(0.92f, 1.06f, gasFlow), run);
        Drive(pump, _pumpVolume * run, PumpPitch[level], run);
        Drive(water, _waterVolume * WaterVolume[level] * Mathf.Lerp(0.5f, 1f, waterFlow) * fault.WaterShare * run, 1f, run);
    }

    private void Drive(AudioSource source, float volume, float pitch, float run)
    {
        float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        source.volume = Mathf.Lerp(source.volume, volume, k);
        source.pitch = Mathf.Lerp(source.pitch, pitch * Mathf.Lerp(stoppedPitch, 1f, run), k);

        if (run > 0f && !source.isPlaying) source.Play();
        else if (run <= 0f && source.volume < 0.001f && source.isPlaying) source.Stop();
    }
}
