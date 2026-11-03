using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Учебный сценарий на установке. Обучение ведёт по шагам и само запускает аварии; экзамен даёт только задачу,
/// устраивает аварию без предупреждения и выставляет оценку.
/// Шаги проверяются по состоянию установки, поэтому подходит любой способ: планшет, рубильник или вентиль.
/// Критерии и штрафы учебные, их нужно сверять с методикой и регламентом по установке.
/// </summary>
public class EmulsifierScenario : MonoBehaviour
{
    public enum ScenarioMode { None, Training, Exam }

    private class Step
    {
        public readonly string Key;
        public readonly Func<bool> Done;
        public readonly Action Enter;
        public readonly float MinTime;

        public Step(string key, Func<bool> done, Action enter = null, float minTime = 0f)
        {
            Key = key;
            Done = done;
            Enter = enter;
            MinTime = minTime;
        }
    }

    private struct Mistake
    {
        public string Key;
        public string Detail;
        public int Penalty;
    }

    private struct FaultRecord
    {
        public EmulsifierFault.FaultType Type;
        public float Reaction;
        public float Duration;
    }

    [SerializeField] private EmulsifierEffects effects;
    [SerializeField] private EmulsifierSettings settings;
    [SerializeField] private EmulsifierCalculation calculation;
    [SerializeField] private EmulsifierGasCleaning cleaning;
    [SerializeField] private EmulsifierFault fault;
    [SerializeField] private EmulsifierFaultRules rules;
    [SerializeField] private EmulsifierSafety safety;
    [SerializeField] private NodeLabelCard card;

    [Tooltip("Норма очистки газа, доля.")]
    [SerializeField] private float targetEfficiency = 0.8f;
    [Tooltip("Сколько показывается «Готово» перед следующим шагом, с.")]
    [SerializeField] private float stepPause = 1.2f;

    [Header("Экзамен")]
    [Tooltip("Сколько установка должна проработать в норме до аварии, с.")]
    [SerializeField] private float faultDelayMin = 20f;
    [SerializeField] private float faultDelayMax = 60f;
    [Tooltip("Сколько установка должна проработать в норме после аварии, чтобы экзамен закончился, с.")]
    [SerializeField] private float finishHoldTime = 8f;
    [Tooltip("Реакция до этого времени не штрафуется: сигнализация срабатывает примерно через 30 с после начала аварии.")]
    [SerializeField] private float goodReaction = 30f;
    [SerializeField] private float slowReaction = 45f;
    [Tooltip("Неисправность, не устранённая за это время, завершает экзамен незачётом: манометр сам по себе не взрывается.")]
    [SerializeField] private float faultTimeLimit = 90f;
    [SerializeField] private int slowPenalty = 10;
    [SerializeField] private int latePenalty = 25;
    [SerializeField] private int settingsPenalty = 20;
    [SerializeField] private int safetyPenalty = 15;
    [SerializeField] private int wrongActionPenalty = 5;
    [SerializeField] private int passScore = 70;

    public ScenarioMode Mode { get; private set; }
    public bool IsRunning => Mode != ScenarioMode.None;

    /// <summary>Текущая задача на выбранном языке: показывается над планшетом на любой вкладке.</summary>
    public string Task
    {
        get
        {
            int percent = Mathf.RoundToInt(targetEfficiency * 100f);
            if (Mode == ScenarioMode.Exam) return L10n.Get("sc.exam.task", percent);
            if (Mode != ScenarioMode.Training) return "";
            string body = _pauseLeft > 0f ? L10n.Get("sc.done") : L10n.Get(_steps[_stepIndex].Key, percent);
            return L10n.Get("sc.step", _stepIndex + 1, _steps.Count) + " " + body;
        }
    }

    /// <summary>Итог последнего прохождения на выбранном языке.</summary>
    public string Report => _report != null ? _report() : "";

    public event Action Started;
    public event Action Changed;
    public event Action Finished;

    private List<Step> _steps;
    private int _stepIndex;
    private float _stepTime;
    private float _pauseLeft;
    private Func<string> _report;

    private bool _rulesWereEnabled;
    private float _examTime;
    private float _startupTime = -1f;
    private float _faultDelay;
    private bool _plannedStarted;
    private bool _plannedHandled;
    private bool _wasPlaying;
    private bool _startPenalized;
    private bool _zonePenalized;
    private float _holdTime;
    private int _score;
    private readonly List<Mistake> _mistakes = new List<Mistake>();
    private readonly List<FaultRecord> _faults = new List<FaultRecord>();

    private bool _inFault;
    private bool _reacted;
    private float _faultStart;
    private float _reactionTime;
    private EmulsifierFault.FaultType _faultType;

    private void Awake()
    {
        Func<bool> stoppedAndLocked = () => !effects.IsPlaying && effects.PowerLocked;
        Func<bool> running = () => effects.IsPlaying;
        Func<bool> faultCleared = () => fault.Fault == EmulsifierFault.FaultType.None;

        _steps = new List<Step>
        {
            new Step("sc.t.node", () => card.Pinned != null),
            new Step("sc.t.ppe", () => safety.AllWorn),
            new Step("sc.t.fluid", () => settings.Fluid == EmulsifierSettings.FluidType.Soda),
            new Step("sc.t.pump", () => settings.PumpLevel == 3),
            new Step("sc.t.valve", () => calculation.WaterFlow >= 0.1f),
            new Step("sc.t.breaker", running),
            new Step("sc.t.smoke", IsWorking, minTime: 6f),

            new Step("sc.t.clog.wait", () => fault.IsAlarm || !effects.IsPlaying, () => fault.StartFault(EmulsifierFault.FaultType.Clog)),
            new Step("sc.t.clog.lock", stoppedAndLocked),
            new Step("sc.t.clog.fix", faultCleared),
            new Step("sc.t.restart", running),

            new Step("sc.t.level.wait", () => fault.IsAlarm || !effects.IsPlaying || faultCleared(), () => fault.StartFault(EmulsifierFault.FaultType.LowLevel)),
            new Step("sc.t.level.fix", faultCleared),
            new Step("sc.t.level.done", () => fault.Level >= 0.99f && effects.IsPlaying),

            new Step("sc.t.pump.lock", stoppedAndLocked, () => fault.StartFault(EmulsifierFault.FaultType.PumpFailure)),
            new Step("sc.t.pump.fix", faultCleared),
            new Step("sc.t.restart", running),

            new Step("sc.t.fan.lock", stoppedAndLocked, () => fault.StartFault(EmulsifierFault.FaultType.FanFailure)),
            new Step("sc.t.fan.fix", faultCleared),
            new Step("sc.t.restart", running),

            new Step("sc.t.gauge.lock", stoppedAndLocked, () => fault.StartFault(EmulsifierFault.FaultType.GaugeFailure)),
            new Step("sc.t.gauge.fix", faultCleared),
            new Step("sc.t.restart", running),
        };

        fault.Exploded += OnExploded;
        safety.ZoneEnteredUnprotected += OnZoneEntered;
    }

    private void OnDestroy()
    {
        fault.Exploded -= OnExploded;
        safety.ZoneEnteredUnprotected -= OnZoneEntered;
    }

    public void StartTraining()
    {
        Begin(ScenarioMode.Training, false);
        EnterStep(0);
    }

    public void StartExam()
    {
        Begin(ScenarioMode.Exam, true);

        _examTime = 0f;
        _startupTime = -1f;
        _faultDelay = UnityEngine.Random.Range(faultDelayMin, faultDelayMax);
        _plannedStarted = false;
        _plannedHandled = false;
        _wasPlaying = false;
        _startPenalized = false;
        _zonePenalized = false;
        _holdTime = 0f;
        _score = 100;
        _mistakes.Clear();
        _faults.Clear();
        _inFault = false;

        Changed?.Invoke();
    }

    public void Cancel()
    {
        if (!IsRunning) return;
        _report = null;
        fault.Restore();
        effects.SetPowerLocked(false);
        End();
        Changed?.Invoke();
    }

    /// <summary>Неверное действие на планшете; считается только на экзамене.</summary>
    public void AddMistake(string key)
    {
        if (Mode == ScenarioMode.Exam) Penalize(key, wrongActionPenalty);
    }

    private void OnZoneEntered()
    {
        if (Mode != ScenarioMode.Exam || _zonePenalized) return;
        _zonePenalized = true;
        Penalize("mis.zone", safetyPenalty);
    }

    private void Begin(ScenarioMode mode, bool rulesEnabled)
    {
        if (!IsRunning) _rulesWereEnabled = rules.RulesEnabled;
        Mode = mode;
        _report = null;
        ResetPlant();
        // На обучении правила выключены: авария от настройки не совпала бы с текстом шага.
        rules.SetEnabled(rulesEnabled);
        Started?.Invoke();
    }

    // Начальные настройки намеренно неверные: насос 1 и малый расход при большом газе нужно исправить до пуска.
    private void ResetPlant()
    {
        effects.Stop();
        fault.Restore();
        safety.ResetAll();
        settings.SetFluid(EmulsifierSettings.FluidType.Water);
        settings.SetPumpLevel(EmulsifierSettings.MinPumpLevel);
        calculation.SetMainGasFlow(EmulsifierCalculation.MainGasFlowOptions[0]);
        calculation.SetGasFlow(14f);
        calculation.SetWaterFlow(0.01f);
        calculation.SetTemperature(15f);
    }

    private void End()
    {
        Mode = ScenarioMode.None;
        rules.SetEnabled(_rulesWereEnabled);
    }

    private void Update()
    {
        if (Mode == ScenarioMode.Training) UpdateTraining();
        else if (Mode == ScenarioMode.Exam) UpdateExam();
    }

    private bool IsWorking() => effects.IsPlaying && fault.Fault == EmulsifierFault.FaultType.None && !fault.IsAlarm
                                && cleaning.Efficiency >= targetEfficiency;

    private void EnterStep(int index)
    {
        _stepIndex = index;
        _stepTime = 0f;
        _pauseLeft = 0f;
        _steps[index].Enter?.Invoke();
        Changed?.Invoke();
    }

    private void UpdateTraining()
    {
        if (_pauseLeft > 0f)
        {
            _pauseLeft -= Time.deltaTime;
            if (_pauseLeft > 0f) return;

            if (_stepIndex + 1 < _steps.Count) EnterStep(_stepIndex + 1);
            else Finish(() => L10n.Get("sc.training.passed"));
            return;
        }

        _stepTime += Time.deltaTime;
        var step = _steps[_stepIndex];
        if (_stepTime < step.MinTime || !step.Done()) return;

        _pauseLeft = stepPause;
        Changed?.Invoke();
    }

    private void UpdateExam()
    {
        _examTime += Time.deltaTime;

        if (effects.IsPlaying && !_wasPlaying && !safety.AllWorn && !_startPenalized)
        {
            _startPenalized = true;
            Penalize("mis.startNoPpe", safetyPenalty);
        }
        _wasPlaying = effects.IsPlaying;

        TrackFault();
        if (!IsRunning) return;

        bool working = IsWorking();
        if (working && _startupTime < 0f) _startupTime = _examTime;

        if (!_plannedStarted)
        {
            if (!working) return;
            _faultDelay -= Time.deltaTime;
            if (_faultDelay > 0f) return;

            _plannedStarted = true;
            fault.StartFault(EmulsifierFault.RandomFault());
            TrackFault();
            return;
        }

        if (!_plannedHandled) return;

        _holdTime = working ? _holdTime + Time.deltaTime : 0f;
        if (_holdTime >= finishHoldTime) FinishExam(null, null);
    }

    // Реакция: первое действие после начала аварии, а не только после сигнала: заметить её можно и раньше, по приборам.
    private void TrackFault()
    {
        var current = fault.Fault;
        if (!_inFault)
        {
            if (current == EmulsifierFault.FaultType.None) return;

            _inFault = true;
            _reacted = false;
            _faultStart = _examTime;
            _faultType = current;
            if (!string.IsNullOrEmpty(fault.Cause)) Penalize("mis.setting", settingsPenalty, fault.Cause);
        }

        if (!_reacted && (!effects.IsPlaying || current == EmulsifierFault.FaultType.None))
        {
            _reacted = true;
            _reactionTime = _examTime - _faultStart;
            if (_reactionTime > slowReaction) Penalize("mis.late", latePenalty);
            else if (_reactionTime > goodReaction) Penalize("mis.slow", slowPenalty);
        }

        if (current != EmulsifierFault.FaultType.None)
        {
            if (_examTime - _faultStart > faultTimeLimit)
                FinishExam("rep.notFixed", EmulsifierFault.NameKey(_faultType));
            return;
        }

        _inFault = false;
        if (_plannedStarted) _plannedHandled = true;
        _faults.Add(new FaultRecord { Type = _faultType, Reaction = _reactionTime, Duration = _examTime - _faultStart });
    }

    private void Penalize(string key, int penalty, string detail = null)
    {
        _score = Mathf.Max(0, _score - penalty);
        _mistakes.Add(new Mistake { Key = key, Detail = detail, Penalty = penalty });
    }

    private void OnExploded()
    {
        if (Mode == ScenarioMode.Training)
        {
            Finish(() => L10n.Get("sc.training.exploded"));
        }
        else if (Mode == ScenarioMode.Exam)
        {
            _score = 0;
            string cause = !string.IsNullOrEmpty(fault.Cause) ? fault.Cause : EmulsifierFault.NameKey(fault.ExplosionCause);
            FinishExam("rep.explosion", cause);
        }
    }

    private void FinishExam(string failureKey, string failureDetail)
    {
        // Копии: при следующем экзамене списки очищаются, а старый итог должен оставаться на экране до его начала.
        int score = _score;
        float startup = _startupTime;
        var mistakes = _mistakes.ToArray();
        var faults = _faults.ToArray();
        Finish(() => BuildReport(score, startup, mistakes, faults, failureKey, failureDetail));
    }

    private void Finish(Func<string> report)
    {
        _report = report;
        End();
        Changed?.Invoke();
        Finished?.Invoke();
    }

    private string BuildReport(int score, float startup, Mistake[] mistakes, FaultRecord[] faults, string failureKey, string failureDetail)
    {
        bool passed = failureKey == null && score >= passScore;
        var lines = new List<string> { L10n.Get(passed ? "rep.pass" : "rep.fail", score) };
        if (failureKey != null) lines.Add(L10n.Get(failureKey, L10n.Get(failureDetail)));
        lines.Add(L10n.Get("rep.startup", startup >= 0f ? FormatTime(startup) : L10n.Get("rep.startup.none")));
        foreach (var record in faults)
            lines.Add(L10n.Get("rep.fault", L10n.Get(EmulsifierFault.NameKey(record.Type)), FormatTime(record.Reaction), FormatTime(record.Duration)));
        foreach (var mistake in mistakes)
        {
            string text = mistake.Detail != null ? L10n.Get(mistake.Key, L10n.Get(mistake.Detail)) : L10n.Get(mistake.Key);
            lines.Add(L10n.Get("rep.penalty", text, mistake.Penalty));
        }
        return string.Join("\n", lines);
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.RoundToInt(seconds);
        return total < 60 ? L10n.Get("rep.time.s", total) : L10n.Get("rep.time.min", total / 60, total % 60);
    }
}
