#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Запускает XR Interaction Simulator (управление с клавиатуры и мыши) в Play Mode,
/// только если шлем не подключён. Встроенный автозапуск XRI так не умеет и мешал бы Quest Link,
/// поэтому в Project Settings он выключен.
/// </summary>
public static class XRSimulatorAutoLoader
{
    // Assets/Samples/XR Interaction Toolkit/<версия>/XR Interaction Simulator/XR Interaction Simulator.prefab
    private const string SimulatorPrefabGuid = "58d0a4ac86f2348deb02f3880c71378e";
    private const string DisableKey = "AirCleaningVR.DisableXRSimulator";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        // XR Management к этому моменту уже инициализирован: если шлем в Link, устройство активно.
        if (XRSettings.isDeviceActive || EditorPrefs.GetBool(DisableKey, false)) return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(SimulatorPrefabGuid));
        if (prefab == null)
        {
            Debug.LogWarning("XR Interaction Simulator prefab not found. Import the sample via Package Manager > XR Interaction Toolkit > Samples.");
            return;
        }

        var simulator = Object.Instantiate(prefab);
        simulator.name = prefab.name;
        Object.DontDestroyOnLoad(simulator);
    }

    [MenuItem("Tools/Air Cleaning VR/Toggle XR Simulator")]
    private static void Toggle()
    {
        bool disabled = !EditorPrefs.GetBool(DisableKey, false);
        EditorPrefs.SetBool(DisableKey, disabled);
        Debug.Log(disabled ? "XR Simulator: выключен" : "XR Simulator: включён (когда шлем не подключён)");
    }
}
#endif
