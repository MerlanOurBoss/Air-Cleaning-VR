using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Переводит перемещение головы (шаги в шлеме или WASD в XR Interaction Simulator) в движение
/// CharacterController. Без этого голова уходит сквозь стены и лестницы: XRBodyTransformer просто
/// ставит капсулу под камеру, не проверяя столкновений. Здесь горизонтальный сдвиг головы
/// снимается с Camera Offset и проходит через CharacterController.Move — тело упирается в препятствия
/// и поднимается по ступеням, а голова остаётся над ним. По высоте камера плавно догоняет тело:
/// ступени и мелкие подвижки капсулы не дёргают картинку в шлеме.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(XROrigin), typeof(CharacterController))]
public class RoomscaleBody : MonoBehaviour
{
    [Tooltip("Насколько тело «прилипает» вниз за шаг, чтобы спускаться по ступеням, м.")]
    [SerializeField] private float snapDown = 0.3f;
    [Tooltip("Сдвиги головы меньше этого (дрожание трекинга) не двигают тело, м.")]
    [SerializeField] private float deadZone = 0.002f;
    [Tooltip("Время, за которое камера догоняет тело по высоте, с.")]
    [SerializeField] private float heightSmoothing = 0.12f;
    [Tooltip("Скачок по высоте больше этого — телепорт: камера переносится сразу, м.")]
    [SerializeField] private float teleportHeight = 1f;

    private XROrigin _origin;
    private CharacterController _body;
    private float _lastHeight;
    private float _heightLag;

    private void Awake()
    {
        _origin = GetComponent<XROrigin>();
        _body = GetComponent<CharacterController>();
        _lastHeight = transform.position.y;
    }

    private void LateUpdate()
    {
        Step();
        SmoothHeight();
    }

    private void SmoothHeight()
    {
        if (_origin.CameraFloorOffsetObject == null) return;

        float height = transform.position.y;
        float jump = height - _lastHeight;
        _lastHeight = height;

        float lag = Mathf.Abs(jump) > teleportHeight ? 0f : _heightLag - jump;
        lag *= Mathf.Exp(-Time.deltaTime / Mathf.Max(heightSmoothing, 0.001f));

        var offset = _origin.CameraFloorOffsetObject.transform;
        var local = offset.localPosition;
        local.y += lag - _heightLag;
        offset.localPosition = local;
        _heightLag = lag;
    }

    public void Step()
    {
        if (_origin.Camera == null || _origin.CameraFloorOffsetObject == null || !_body.enabled) return;

        var head = _origin.Camera.transform.position - transform.position;
        head.y = 0f;
        if (head.sqrMagnitude < deadZone * deadZone) return;

        // Возвращаем голову над центром рига, а сдвиг отдаём телу.
        _origin.CameraFloorOffsetObject.transform.position -= head;
        var center = _body.center;
        _body.center = new Vector3(0f, center.y, 0f);
        _body.Move(head + Vector3.down * snapDown);
    }
}
