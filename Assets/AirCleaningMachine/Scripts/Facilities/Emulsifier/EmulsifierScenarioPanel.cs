using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Страница «Обучение» на планшете: выбор режима и итог. Текущая задача показывается в отдельной полосе
/// над экраном: действия выполняются на других вкладках, и задача не должна при этом пропадать.
/// </summary>
public class EmulsifierScenarioPanel : MonoBehaviour
{
    private const string Intro = "<b>Обучение</b>: пошаговые подсказки и две учебные аварии.\n"
                                 + "<b>Экзамен</b>: подсказок нет, авария начнётся внезапно, в конце будет оценка.\n"
                                 + "Оба режима сбрасывают настройки установки.";
    private const string Running = "Сценарий идёт. Задача показана над планшетом.";

    [SerializeField] private EmulsifierScenario scenario;
    [SerializeField] private TabletTabs tabs;
    [Tooltip("Номер вкладки этой страницы: на неё планшет переходит, когда появился итог.")]
    [SerializeField] private int pageIndex = 3;

    [SerializeField] private Button trainingButton;
    [SerializeField] private Button examButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text info;
    [SerializeField] private GameObject taskStrip;
    [SerializeField] private TMP_Text taskText;

    private void Start()
    {
        // Префаб планшета не может ссылаться на объекты сцены.
        if (scenario == null) scenario = FindAnyObjectByType<EmulsifierScenario>();

        trainingButton.onClick.AddListener(scenario.StartTraining);
        examButton.onClick.AddListener(scenario.StartExam);
        cancelButton.onClick.AddListener(scenario.Cancel);

        scenario.Changed += Refresh;
        scenario.Finished += ShowPage;
        Refresh();
    }

    private void OnDestroy()
    {
        if (scenario == null) return;
        scenario.Changed -= Refresh;
        scenario.Finished -= ShowPage;
    }

    private void ShowPage() => tabs.Show(pageIndex);

    private void Refresh()
    {
        bool running = scenario.IsRunning;
        trainingButton.interactable = !running;
        examButton.interactable = !running;
        cancelButton.interactable = running;

        info.text = running ? Running : string.IsNullOrEmpty(scenario.Report) ? Intro : scenario.Report;
        taskStrip.SetActive(running);
        taskText.text = scenario.Task;
    }
}
