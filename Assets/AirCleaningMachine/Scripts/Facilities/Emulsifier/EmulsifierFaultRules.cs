using System;
using UnityEngine;

/// <summary>
/// Аварии от неправильной настройки: опасное сочетание на планшете, удержанное несколько секунд,
/// запускает аварию в EmulsifierFault. Пороги учебные, их нужно сверять с методикой по установке.
/// </summary>
public class EmulsifierFaultRules : MonoBehaviour
{
    private class Rule
    {
        public readonly EmulsifierFault.FaultType Type;
        public readonly string Cause;
        public readonly Func<EmulsifierSettings, EmulsifierCalculation, bool> IsBroken;

        public Rule(EmulsifierFault.FaultType type, string cause, Func<EmulsifierSettings, EmulsifierCalculation, bool> isBroken)
        {
            Type = type;
            Cause = cause;
            IsBroken = isBroken;
        }
    }

    // Проверяются сверху вниз, срабатывает первое нарушенное правило.
    private static readonly Rule[] Rules =
    {
        new Rule(EmulsifierFault.FaultType.Clog, "rule.sodaCold",
            (s, c) => s.Fluid == EmulsifierSettings.FluidType.Soda && c.Temperature <= 5f),
        new Rule(EmulsifierFault.FaultType.Clog, "rule.lowIrrigation",
            (s, c) => c.GasFlow >= 14f && c.WaterFlow <= 0.01f),
        new Rule(EmulsifierFault.FaultType.LowLevel, "rule.pumpSlow",
            (s, c) => s.PumpLevel <= 1 && c.WaterFlow >= 0.1f),
        new Rule(EmulsifierFault.FaultType.LowLevel, "rule.pumpWeak",
            (s, c) => s.PumpLevel <= 1 && c.MainGasFlow >= 400000f),
    };

    [SerializeField] private EmulsifierFault fault;
    [SerializeField] private EmulsifierEffects effects;
    [SerializeField] private EmulsifierSettings settings;
    [SerializeField] private EmulsifierCalculation calculation;
    [Tooltip("Сколько неверная настройка должна продержаться на работающей установке, с: случайное нажатие не ломает установку.")]
    [SerializeField] private float holdTime = 12f;
    [SerializeField] private bool rulesEnabled = true;

    public bool RulesEnabled => rulesEnabled;

    private Rule _held;
    private float _heldTime;

    public void Toggle()
    {
        rulesEnabled = !rulesEnabled;
    }

    public void SetEnabled(bool enabled)
    {
        rulesEnabled = enabled;
        _held = null;
        _heldTime = 0f;
    }

    private void Update()
    {
        var rule = rulesEnabled ? FindBroken() : null;
        if (rule == null)
        {
            _held = null;
            _heldTime = 0f;
            fault.SettingsCorrected();
            return;
        }

        if (rule != _held)
        {
            _held = rule;
            _heldTime = 0f;
        }

        if (!effects.IsPlaying) return;

        _heldTime += Time.deltaTime;
        if (_heldTime >= holdTime) fault.StartFromSettings(rule.Type, rule.Cause);
    }

    private Rule FindBroken()
    {
        foreach (var rule in Rules)
            if (rule.IsBroken(settings, calculation)) return rule;
        return null;
    }
}
