using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Переводит перемещение головы (шаги в шлеме или WASD в XR Interaction Simulator) в движение
/// CharacterController. Без этого голова уходит сквозь стены и лестницы: XRBodyTransformer просто
/// ставит капсулу под камеру, не проверяя столкновений. Здесь горизонтальный сдвиг головы
/// снимается с Camera Offset и проходит через CharacterController.Move — тело упирается в препятствия
/// и поднимается по ступеням, а голова остаётся над ним.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(XROrigin), typeof(CharacterController))]
public class RoomscaleBody : MonoBehaviour
{
    [Tooltip("Насколько тело «прилипает» вниз за шаг, чтобы спускаться по ступеням, м.")]
    [SerializeField] private float snapDown = 0.3f;
    [Tooltip("Сдвиги головы меньше этого (дрожание трекинга) не двигают тело, м.")]
    [SerializeField] private float deadZone = 0.002f;

    private XROrigin _origin;
    private CharacterController _body;

    private void Awake()
    {
        _origin = GetComponent<XROrigin>();
        _body = GetComponent<CharacterController>();
    }

    private void LateUpdate() => Step();

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
