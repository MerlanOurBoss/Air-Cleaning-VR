using System;
using UnityEngine;

/// <summary>
/// Учебная авария эмульгатора. Засор поднимает давление на манометре, утечка опускает уровень в уровнемере,
/// а уровень ниже минимума тоже ведёт к росту давления. При давлении разрыва происходит взрыв.
/// Давление растёт только при работающей установке: «Стоп» — правильное первое действие при аварии.
/// </summary>
public class EmulsifierFault : MonoBehaviour
{
    public enum FaultType { None, Clog, LowLevel }

    [SerializeField] private EmulsifierEffects effects;

    [Header("Давление, кПа")]
    [SerializeField] private float normalPressure = 2.5f;
    [SerializeField] private float warningPressure = 6f;
    [SerializeField] private float burstPressure = 10f;
    [Tooltip("Время роста давления от нормы до разрыва, с.")]
    [SerializeField] private float riseTime = 60f;
    [Tooltip("Время падения давления при остановке, с.")]
    [SerializeField] private float reliefTime = 5f;

    [Header("Уровень, доля от нормы")]
    [Tooltip("Отметка минимума на шкале уровнемера.")]
    [SerializeField] private float minLevel = 0.27f;
    [Tooltip("Время падения уровня от нормы до нуля, с.")]
    [SerializeField] private float drainTime = 40f;
    [Tooltip("Время доливки от нуля до нормы, с.")]
    [SerializeField] private float refillTime = 6f;
    [Tooltip("Уровень, ниже которого форсунки начинают пересыхать. На отметке минимума вода прекращается совсем.")]
    [SerializeField] private float waterFullLevel = 0.7f;

    [Header("Случайная авария, с")]
    [SerializeField] private float randomDelayMin = 20f;
    [SerializeField] private float randomDelayMax = 60f;

    [Header("Приборы")]
    [SerializeField] private Transform needle;
    [Tooltip("Давление в конце шкалы манометра, кПа.")]
    [SerializeField] private float gaugeMaxPressure = 12f;
    [Tooltip("Угол стрелки в начале шкалы; конец шкалы — такой же угол с другой стороны.")]
    [SerializeField] private float needleZeroAngle = -135f;
    [SerializeField] private Transform liquid;
    [SerializeField] private Transform meniscus;
    [SerializeField] private Renderer lamp;
    [SerializeField] private Color lampColor = new Color(4f, 0.15f, 0.08f);
    [SerializeField] private float lampFlashRate = 2f;

    [Header("Звук")]
    [SerializeField] private AudioSource siren;
    [SerializeField] private AudioSource hiss;

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public FaultType Fault { get; private set; }
    public float Pressure { get; private set; }
    public float Level { get; private set; } = 1f;
    public bool HasExploded { get; private set; }
    /// <summary>Что вызвало взрыв: остаётся заполненным и после него, чтобы показать причину.</summary>
    public FaultType ExplosionCause { get; private set; }
    /// <summary>Неправильная настройка, из-за которой началась авария; пусто у аварий, запущенных вручную.</summary>
    public string Cause { get; private set; } = "";

    /// <summary>Доля подачи воды: насосу нечего качать, когда уровень упал до минимума.</summary>
    public float WaterShare => Mathf.InverseLerp(minLevel, waterFullLevel, Level);
    public float WarningPressure => warningPressure;
    public float MinLevel => minLevel;
    public bool IsAlarm => !HasExploded && (Pressure >= warningPressure || Level <= minLevel);

    public event Action Changed;
    public event Action Exploded;
    public event Action Restored;

    private FaultType _pending;
    private float _pendingDelay;
    private bool _refilling;
    private bool _fromSettings;
    // Тревога уже была: одним исправлением настройки аварию не снять, даже когда давление упало после «Стоп».
    private bool _alarmRaised;

    private float _liquidBottom, _liquidHeight;
    private float _hissVolume;
    private float _appliedWaterShare = 1f;
    private Material _lampMaterial;

    private void Awake()
    {
        // Цилиндр Unity высотой 2: scale.y — половина высоты столба.
        _liquidBottom = liquid.localPosition.y - liquid.localScale.y;
        _liquidHeight = liquid.localScale.y * 2f;
        _hissVolume = hiss.volume;
        hiss.volume = 0f;

        _lampMaterial = new Material(lamp.sharedMaterial);
        // URP снимает этот ключ с материала, у которого в ассете чёрное свечение.
        _lampMaterial.EnableKeyword("_EMISSION");
        lamp.sharedMaterial = _lampMaterial;
    }

    private void OnDestroy()
    {
        Destroy(_lampMaterial);
    }

    private void Update()
    {
        if (!HasExploded) Simulate(Time.deltaTime);
        ApplyInstruments();

        if (!Mathf.Approximately(_appliedWaterShare, WaterShare))
        {
            _appliedWaterShare = WaterShare;
            effects.SetWaterShare(_appliedWaterShare);
        }
    }

    private void Simulate(float dt)
    {
        bool running = effects.IsPlaying;

        if (_pendingDelay > 0f && running)
        {
            _pendingDelay -= dt;
            if (_pendingDelay <= 0f) Begin(_pending);
        }

        if (_refilling)
        {
            Level = Mathf.MoveTowards(Level, 1f, dt / Mathf.Max(refillTime, 0.01f));
            if (Level >= 1f) _refilling = false;
        }
        else if (Fault == FaultType.LowLevel && running)
        {
            Level = Mathf.MoveTowards(Level, 0f, dt / Mathf.Max(drainTime, 0.01f));
        }

        bool rising = running && (Fault == FaultType.Clog || (Fault == FaultType.LowLevel && Level <= minLevel));
        if (rising)
            Pressure += (burstPressure - normalPressure) / Mathf.Max(riseTime, 0.01f) * dt;
        else
            Pressure = Mathf.MoveTowards(Pressure, running ? normalPressure : 0f, burstPressure / Mathf.Max(reliefTime, 0.01f) * dt);

        if (IsAlarm && Fault != FaultType.None) _alarmRaised = true;
        if (Pressure >= burstPressure) Explode();
    }

    private void ApplyInstruments()
    {
        float scale = Mathf.Clamp01(Pressure / gaugeMaxPressure);
        // Лёгкое дрожание стрелки у работающей установки: прибор выглядит живым.
        float jitter = effects.IsPlaying ? (Mathf.PerlinNoise(Time.time * 6f, 0f) - 0.5f) * 1.5f : 0f;
        needle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(needleZeroAngle, -needleZeroAngle, scale) + jitter);

        float height = _liquidHeight * Level;
        var scaleY = liquid.localScale;
        scaleY.y = height * 0.5f;
        liquid.localScale = scaleY;
        var position = liquid.localPosition;
        position.y = _liquidBottom + height * 0.5f;
        liquid.localPosition = position;
        position = meniscus.localPosition;
        position.y = _liquidBottom + height;
        meniscus.localPosition = position;

        bool flash = (IsAlarm || HasExploded) && Mathf.Repeat(Time.time * lampFlashRate, 1f) < 0.5f;
        _lampMaterial.SetColor(EmissionColor, flash ? lampColor : Color.black);

        if (IsAlarm && !siren.isPlaying) siren.Play();
        else if (!IsAlarm && siren.isPlaying) siren.Stop();

        float pressureShare = effects.IsPlaying ? Mathf.InverseLerp(normalPressure, burstPressure, Pressure) : 0f;
        hiss.volume = _hissVolume * pressureShare;
        if (pressureShare > 0f && !hiss.isPlaying) hiss.Play();
        else if (pressureShare <= 0f && hiss.isPlaying) hiss.Stop();
    }

    public void StartClog() => Begin(FaultType.Clog);

    public void StartLowLevel() => Begin(FaultType.LowLevel);

    /// <summary>Авария случится позже и без объявления: её нужно заметить по приборам.</summary>
    public void StartRandom()
    {
        if (HasExploded) return;
        _pending = UnityEngine.Random.value < 0.5f ? FaultType.Clog : FaultType.LowLevel;
        _pendingDelay = UnityEngine.Random.Range(randomDelayMin, randomDelayMax);
        Changed?.Invoke();
    }

    private void Begin(FaultType type, string cause = "", bool fromSettings = false)
    {
        if (HasExploded) return;
        Fault = type;
        Cause = cause;
        _fromSettings = fromSettings;
        _alarmRaised = false;
        _pendingDelay = 0f;
        _refilling = false;
        Changed?.Invoke();
    }

    /// <summary>Авария от неправильной настройки; уже идущую аварию не подменяет.</summary>
    public void StartFromSettings(FaultType type, string cause)
    {
        if (Fault == FaultType.None) Begin(type, cause, true);
    }

    /// <summary>Настройку исправили. До тревоги этого достаточно: приборы сами возвращаются в норму.</summary>
    public void SettingsCorrected()
    {
        if (Fault == FaultType.None || !_fromSettings || _alarmRaised) return;
        Fault = FaultType.None;
        Cause = "";
        _refilling = Level < 1f;
        Changed?.Invoke();
    }

    /// <summary>Прочистка возможна только на остановленной установке.</summary>
    public bool TryClean()
    {
        if (Fault != FaultType.Clog || effects.IsPlaying) return false;
        Fault = FaultType.None;
        Changed?.Invoke();
        return true;
    }

    public bool TryRefill()
    {
        if (HasExploded || Fault != FaultType.LowLevel) return false;
        Fault = FaultType.None;
        _refilling = true;
        Changed?.Invoke();
        return true;
    }

    private void Explode()
    {
        HasExploded = true;
        ExplosionCause = Fault;
        Fault = FaultType.None;
        _pendingDelay = 0f;
        effects.Stop();
        Exploded?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>Возвращает установку в исходное состояние после взрыва; запускает её сам пользователь.</summary>
    public void Restore()
    {
        HasExploded = false;
        ExplosionCause = FaultType.None;
        Cause = "";
        Fault = FaultType.None;
        Pressure = 0f;
        Level = 1f;
        _refilling = false;
        Restored?.Invoke();
        Changed?.Invoke();
    }
}
