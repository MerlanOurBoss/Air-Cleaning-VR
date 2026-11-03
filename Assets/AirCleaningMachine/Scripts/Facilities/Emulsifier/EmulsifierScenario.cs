using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Учебный сценарий на установке. Обучение ведёт по шагам и само запускает аварии; экзамен даёт только задачу,
/// устраивает аварию без предупреждения и выставляет оценку.
/// Шаги проверяются по состоянию установки, поэтому подходит любой способ: планшет, рубильник или вентиль.
/// Критерии и штрафы учебные, их нужно сверять с методикой по установке.
/// </summary>
public class EmulsifierScenario : MonoBehaviour
{
    public enum ScenarioMode { None, Training, Exam }

    private class Step
    {
        public readonly string Text;
        public readonly Func<bool> Done;
        public readonly Action Enter;
        public readonly float MinTime;

        public Step(string text, Func<bool> done, Action enter = null, float minTime = 0f)
        {
            Text = text;
            Done = done;
            Enter = enter;
            MinTime = minTime;
        }
    }

    [SerializeField] private EmulsifierEffects effects;
    [SerializeField] private EmulsifierSettings settings;
    [SerializeField] private EmulsifierCalculation calculation;
    [SerializeField] private EmulsifierGasCleaning cleaning;
    [SerializeField] private EmulsifierFault fault;
    [SerializeField] private EmulsifierFaultRules rules;
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
    [SerializeField] private int slowPenalty = 10;
    [SerializeField] private int latePenalty = 25;
    [SerializeField] private int settingsPenalty = 20;
    [SerializeField] private int wrongActionPenalty = 5;
    [SerializeField] private int passScore = 70;

    public ScenarioMode Mode { get; private set; }
    public bool IsRunning => Mode != ScenarioMode.None;
    /// <summary>Текущая задача: показывается над планшетом на любой вкладке.</summary>
    public string Task { get; private set; } = "";
    /// <summary>Итог последнего прохождения.</summary>
    public string Report { get; private set; } = "";

    public event Action Started;
    public event Action Changed;
    public event Action Finished;

    private List<Step> _steps;
    private int _stepIndex;
    private float _stepTime;
    private float _pauseLeft;

    private bool _rulesWereEnabled;
    private float _examTime;
    private float _startupTime = -1f;
    private float _faultDelay;
    private bool _plannedStarted;
    private bool _plannedHandled;
    private float _holdTime;
    private int _score;
    private readonly List<string> _mistakes = new List<string>();
    private readonly List<string> _faultLines = new List<string>();

    private bool _inFault;
    private bool _reacted;
    private float _faultStart;
    private float _reactionTime;
    private EmulsifierFault.FaultType _faultType;

    private void Awake()
    {
        _steps = new List<Step>
        {
            new Step("Наведите луч на любой узел установки: появится рамка. Нажмите курок, чтобы открыть описание.",
                () => card.Pinned != null),
            new Step("Вкладка «Управление»: выберите жидкость «Сода».",
                () => settings.Fluid == EmulsifierSettings.FluidType.Soda),
            new Step("На той же вкладке поставьте насос на уровень 3.",
                () => settings.PumpLevel == 3),
            new Step("Подойдите к посту ручного управления и вентилем выставьте расход жидкости 0,10 м³/с.",
                () => calculation.WaterFlow >= 0.1f),
            new Step("Включите установку рубильником на посту.",
                () => effects.IsPlaying),
            new Step("Посмотрите на дым: на выходе из колонны он светлеет. Установка должна работать с очисткой не ниже "
                     + Mathf.RoundToInt(targetEfficiency * 100f) + " %.",
                IsWorking, minTime: 6f),
            new Step("Учебная авария. Следите за манометром: давление растёт. Дождитесь сигнала тревоги.",
                () => fault.IsAlarm || !effects.IsPlaying, fault.StartClog),
            new Step("Давление выше нормы: это засор. Немедленно остановите установку.",
                () => !effects.IsPlaying),
            new Step("Вкладка «Авария»: нажмите «Прочистить».",
                () => fault.Fault == EmulsifierFault.FaultType.None),
            new Step("Засор устранён. Снова запустите установку.",
                () => effects.IsPlaying),
            new Step("Новая авария. Следите за уровнемером: уровень падает. Дождитесь сигнала тревоги.",
                () => fault.IsAlarm || !effects.IsPlaying || fault.Fault == EmulsifierFault.FaultType.None, fault.StartLowLevel),
            new Step("Уровень ниже минимума. Вкладка «Авария»: нажмите «Долить».",
                () => fault.Fault == EmulsifierFault.FaultType.None),
            new Step("Идёт доливка. Дождитесь нормы; если установка остановлена, запустите её.",
                () => fault.Level >= 0.99f && effects.IsPlaying),
        };

        fault.Exploded += OnExploded;
    }

    private void OnDestroy()
    {
        fault.Exploded -= OnExploded;
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
        _holdTime = 0f;
        _score = 100;
        _mistakes.Clear();
        _faultLines.Clear();
        _inFault = false;

        Task = "Экзамен: запустите установку с очисткой не ниже " + Mathf.RoundToInt(targetEfficiency * 100f)
               + " % и следите за её работой по приборам.";
        Changed?.Invoke();
    }

    public void Cancel()
    {
        if (!IsRunning) return;
        Report = "";
        fault.Restore();
        End();
        Changed?.Invoke();
    }

    /// <summary>Неверное действие на планшете; считается только на экзамене.</summary>
    public void AddMistake(string text)
    {
        if (Mode == ScenarioMode.Exam) Penalize(text, wrongActionPenalty);
    }

    private void Begin(ScenarioMode mode, bool rulesEnabled)
    {
        if (!IsRunning) _rulesWereEnabled = rules.RulesEnabled;
        Mode = mode;
        Report = "";
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
        Task = "";
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
        Task = StepPrefix() + _steps[index].Text;
        _steps[index].Enter?.Invoke();
        Changed?.Invoke();
    }

    private string StepPrefix() => "Шаг " + (_stepIndex + 1) + " из " + _steps.Count + ". ";

    private void UpdateTraining()
    {
        if (_pauseLeft > 0f)
        {
            _pauseLeft -= Time.deltaTime;
            if (_pauseLeft > 0f) return;

            if (_stepIndex + 1 < _steps.Count) EnterStep(_stepIndex + 1);
            else Finish("<b>Обучение пройдено.</b>\nТеперь можно сдать экзамен: подсказок не будет, авария начнётся без предупреждения.");
            return;
        }

        _stepTime += Time.deltaTime;
        var step = _steps[_stepIndex];
        if (_stepTime < step.MinTime || !step.Done()) return;

        Task = StepPrefix() + "<color=#6FDC8C>Готово</color>";
        _pauseLeft = stepPause;
        Changed?.Invoke();
    }

    private void UpdateExam()
    {
        _examTime += Time.deltaTime;
        TrackFault();

        bool working = IsWorking();
        if (working && _startupTime < 0f) _startupTime = _examTime;

        if (!_plannedStarted)
        {
            if (!working) return;
            _faultDelay -= Time.deltaTime;
            if (_faultDelay > 0f) return;

            _plannedStarted = true;
            if (UnityEngine.Random.value < 0.5f) fault.StartClog();
            else fault.StartLowLevel();
            TrackFault();
            return;
        }

        if (!_plannedHandled) return;

        _holdTime = working ? _holdTime + Time.deltaTime : 0f;
        if (_holdTime >= finishHoldTime) Finish(BuildReport(""));
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
            if (!string.IsNullOrEmpty(fault.Cause)) Penalize("Неверная настройка. " + fault.Cause, settingsPenalty);
        }

        if (!_reacted && (!effects.IsPlaying || current == EmulsifierFault.FaultType.None))
        {
            _reacted = true;
            _reactionTime = _examTime - _faultStart;
            if (_reactionTime > slowReaction) Penalize("Поздняя реакция на аварию", latePenalty);
            else if (_reactionTime > goodReaction) Penalize("Медленная реакция на аварию", slowPenalty);
        }

        if (current != EmulsifierFault.FaultType.None) return;

        _inFault = false;
        if (_plannedStarted) _plannedHandled = true;
        _faultLines.Add("Авария «" + FaultName(_faultType) + "»: реакция " + FormatTime(_reactionTime)
                        + ", устранена за " + FormatTime(_examTime - _faultStart));
    }

    private void Penalize(string text, int penalty)
    {
        _score = Mathf.Max(0, _score - penalty);
        _mistakes.Add(text + ": −" + penalty);
    }

    private void OnExploded()
    {
        if (Mode == ScenarioMode.Training)
        {
            Finish("<color=#FF5A4A><b>Взрыв.</b></color> Давление дошло до разрыва, пока установка работала.\nНачните обучение заново: при тревоге останавливайте установку сразу.");
        }
        else if (Mode == ScenarioMode.Exam)
        {
            _score = 0;
            Finish(BuildReport("Взрыв: " + (!string.IsNullOrEmpty(fault.Cause) ? fault.Cause : FaultName(fault.ExplosionCause))));
        }
    }

    private void Finish(string report)
    {
        Report = report;
        End();
        Changed?.Invoke();
        Finished?.Invoke();
    }

    private string BuildReport(string failure)
    {
        bool passed = string.IsNullOrEmpty(failure) && _score >= passScore;
        var lines = new List<string>
        {
            (passed ? "<color=#6FDC8C><b>Зачёт: " : "<color=#FF5A4A><b>Незачёт: ") + _score + " из 100</b></color>"
        };
        if (!string.IsNullOrEmpty(failure)) lines.Add(failure);
        lines.Add("Выход на норму: " + (_startupTime >= 0f ? FormatTime(_startupTime) : "не выполнен"));
        lines.AddRange(_faultLines);
        lines.AddRange(_mistakes);
        return string.Join("\n", lines);
    }

    private static string FaultName(EmulsifierFault.FaultType type) =>
        type == EmulsifierFault.FaultType.Clog ? "засор" : "низкий уровень";

    private static string FormatTime(float seconds)
    {
        int total = Mathf.RoundToInt(seconds);
        return total < 60 ? total + " с" : total / 60 + " мин " + total % 60 + " с";
    }
}
