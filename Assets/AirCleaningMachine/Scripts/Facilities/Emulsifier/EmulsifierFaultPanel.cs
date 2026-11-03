using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Страница «Авария» на планшете: запуск учебной аварии, показания приборов и действия по устранению.
/// О начале аварии панель не сообщает, пока не сработала сигнализация: её нужно заметить по приборам.
/// Обслуживание идёт по порядку безопасности: установка остановлена, рубильник заблокирован, СИЗ надеты.
/// </summary>
public class EmulsifierFaultPanel : MonoBehaviour
{
    [SerializeField] private EmulsifierFault fault;
    [SerializeField] private EmulsifierEffects effects;
    [SerializeField] private EmulsifierFaultRules rules;
    [SerializeField] private EmulsifierScenario scenario;
    [SerializeField] private EmulsifierSafety safety;

    [SerializeField] private Button randomButton;
    [SerializeField] private Button clogButton;
    [SerializeField] private Button lowLevelButton;
    [SerializeField] private Button pumpButton;
    [SerializeField] private Button fanButton;
    [SerializeField] private Button gaugeButton;
    [SerializeField] private Button cleanButton;
    [SerializeField] private Button refillButton;
    [SerializeField] private Button repairPumpButton;
    [SerializeField] private Button repairFanButton;
    [SerializeField] private Button replaceGaugeButton;
    [SerializeField] private Button restoreButton;
    [SerializeField] private Button rulesButton;
    [SerializeField] private TMP_Text rulesLabel;
    [Tooltip("Кнопка «Пуск/Стоп»: после взрыва и при блокировке запуск закрыт.")]
    [SerializeField] private Button powerButton;
    [SerializeField] private TMP_Text readout;
    [SerializeField] private TMP_Text message;
    [SerializeField] private Color normalColor = new Color(0.18f, 0.18f, 0.18f);
    [SerializeField] private Color activeColor = new Color(0.125f, 0.588f, 0.953f);

    private Func<string> _messageSource;
    private string _message = "";
    private string _pressureAlarm = "";
    private string _levelAlarm = "";
    private int _shownPressure = -1;
    private int _shownLevel = -1;
    private int _shownWorn = -1;

    private void Start()
    {
        // Префаб планшета не может ссылаться на объекты сцены.
        if (fault == null) fault = FindAnyObjectByType<EmulsifierFault>();
        if (effects == null) effects = FindAnyObjectByType<EmulsifierEffects>();
        if (rules == null) rules = FindAnyObjectByType<EmulsifierFaultRules>();
        if (scenario == null) scenario = FindAnyObjectByType<EmulsifierScenario>();
        if (safety == null) safety = FindAnyObjectByType<EmulsifierSafety>();

        randomButton.onClick.AddListener(() =>
        {
            fault.StartRandom();
            Say(() => L10n.Get("msg.random"));
        });
        BindDrill(clogButton, EmulsifierFault.FaultType.Clog);
        BindDrill(lowLevelButton, EmulsifierFault.FaultType.LowLevel);
        BindDrill(pumpButton, EmulsifierFault.FaultType.PumpFailure);
        BindDrill(fanButton, EmulsifierFault.FaultType.FanFailure);
        BindDrill(gaugeButton, EmulsifierFault.FaultType.GaugeFailure);

        cleanButton.onClick.AddListener(() => Repair(EmulsifierFault.FaultType.Clog));
        repairPumpButton.onClick.AddListener(() => Repair(EmulsifierFault.FaultType.PumpFailure));
        repairFanButton.onClick.AddListener(() => Repair(EmulsifierFault.FaultType.FanFailure));
        replaceGaugeButton.onClick.AddListener(() => Repair(EmulsifierFault.FaultType.GaugeFailure));
        refillButton.onClick.AddListener(Refill);

        rulesButton.onClick.AddListener(() =>
        {
            rules.Toggle();
            Refresh();
        });
        restoreButton.onClick.AddListener(() =>
        {
            fault.Restore();
            Say(() => L10n.Get("msg.restored"));
        });

        fault.Changed += Refresh;
        effects.StateChanged += Refresh;
        scenario.Changed += Refresh;
        scenario.Started += ClearMessage;
        L10n.Changed += OnLanguageChanged;
        OnLanguageChanged();
    }

    private void OnDestroy()
    {
        L10n.Changed -= OnLanguageChanged;
        if (fault != null) fault.Changed -= Refresh;
        if (effects != null) effects.StateChanged -= Refresh;
        if (scenario == null) return;
        scenario.Changed -= Refresh;
        scenario.Started -= ClearMessage;
    }

    private void BindDrill(Button button, EmulsifierFault.FaultType type)
    {
        button.onClick.AddListener(() =>
        {
            fault.StartFault(type);
            Say(() => L10n.Get("msg.drill", L10n.Get(EmulsifierFault.NameKey(type))));
        });
    }

    private void Update()
    {
        // Текст меняем только при изменении: сборка строк каждый кадр создаёт мусор и паузы сборщика в шлеме.
        int pressure = Mathf.RoundToInt(fault.Pressure * 10f);
        int level = Mathf.RoundToInt(fault.Level * 100f);
        int worn = safety.WornCount;
        if (pressure != _shownPressure || level != _shownLevel || worn != _shownWorn)
        {
            _shownPressure = pressure;
            _shownLevel = level;
            _shownWorn = worn;
            readout.text = L10n.Get("fault.readout", L10n.Number(fault.Pressure, "0.0"), level, worn, safety.ItemCount);
        }

        if (fault.HasExploded) return;
        string current = !fault.IsAlarm ? _message
                       : fault.Pressure >= fault.WarningPressure ? _pressureAlarm : _levelAlarm;
        if (message.text != current) message.text = current;
    }

    private void Repair(EmulsifierFault.FaultType type)
    {
        if (fault.Fault != type) Refuse("msg.noFault", "mis.noFault");
        else if (effects.IsPlaying) Refuse("msg.stopFirst", "mis.running");
        else if (!effects.PowerLocked) Refuse("msg.lockFirst", "mis.noLock");
        else if (!safety.AllWorn) Refuse("msg.ppeFirst", "mis.noPpe");
        else
        {
            string cause = fault.Cause;
            if (fault.TryRepair(type)) Say(() => L10n.Get("msg.fixed") + " " + CauseHint(cause, "msg.fixed.restart"));
        }
    }

    private void Refill()
    {
        if (fault.Fault != EmulsifierFault.FaultType.LowLevel) Refuse("msg.noFault", "mis.noFault");
        else if (!safety.AllWorn) Refuse("msg.ppeFirst", "mis.noPpe");
        else
        {
            string cause = fault.Cause;
            if (fault.TryRefill()) Say(() => L10n.Get("msg.refilling") + " " + CauseHint(cause, null));
        }
    }

    private void Refuse(string messageKey, string mistakeKey)
    {
        Say(() => L10n.Get(messageKey));
        scenario.AddMistake(mistakeKey);
    }

    // После устранения показываем, какую настройку поправить, иначе авария начнётся снова. На экзамене не подсказываем.
    private string CauseHint(string cause, string otherwiseKey)
    {
        if (!string.IsNullOrEmpty(cause) && scenario.Mode != EmulsifierScenario.ScenarioMode.Exam)
            return L10n.Get("msg.fixSetting", L10n.Get(cause));
        return otherwiseKey != null ? L10n.Get(otherwiseKey) : "";
    }

    private void Say(Func<string> source)
    {
        _messageSource = source;
        _message = source != null ? source() : "";
    }

    private void ClearMessage() => Say(null);

    private void OnLanguageChanged()
    {
        _pressureAlarm = L10n.Get("fault.alarm.pressure");
        _levelAlarm = L10n.Get("fault.alarm.level");
        _shownPressure = -1;
        Say(_messageSource);
        Refresh();
    }

    private void Refresh()
    {
        powerButton.interactable = !fault.HasExploded && !effects.PowerLocked;
        restoreButton.interactable = fault.HasExploded;
        cleanButton.interactable = !fault.HasExploded;
        refillButton.interactable = !fault.HasExploded;
        repairPumpButton.interactable = !fault.HasExploded;
        repairFanButton.interactable = !fault.HasExploded;
        replaceGaugeButton.interactable = !fault.HasExploded;

        // Во время сценария аварии запускает он сам, а правила не отключаются.
        bool free = !fault.HasExploded && !scenario.IsRunning;
        randomButton.interactable = free;
        clogButton.interactable = free;
        lowLevelButton.interactable = free;
        pumpButton.interactable = free;
        fanButton.interactable = free;
        gaugeButton.interactable = free;
        rulesButton.interactable = !scenario.IsRunning;

        rulesLabel.text = L10n.Get(rules.RulesEnabled ? "fault.rules.on" : "fault.rules.off");
        EmulsifierTablet.Tint(rulesButton, rules.RulesEnabled ? activeColor : normalColor);

        if (fault.HasExploded)
            message.text = L10n.Get("fault.explosion", L10n.Get(!string.IsNullOrEmpty(fault.Cause) ? fault.Cause : EmulsifierFault.NameKey(fault.ExplosionCause)));
    }
}
