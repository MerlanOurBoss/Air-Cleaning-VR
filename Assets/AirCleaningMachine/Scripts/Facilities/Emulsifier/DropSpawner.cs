using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DropSpawner : MonoBehaviour
{
    public GameObject prefabToSpawn;
    public GameObject spawnAreaCube;
    public float spawnInterval = 0.001f;
    public float _delay;
    public float dropScale = 1f;
    // Множитель радиуса коллайдера: каплю можно увеличить визуально, не меняя физику
    // (зоны спавна касаются стенок бака, и крупный коллайдер гасит каплю сразу).
    public float dropColliderScale = 1f;
    // Множитель гравитации для капель. Уменьшенная установка с настоящей гравитацией
    // роняет капли за доли секунды; = масштабу установки возвращает темп оригинала.
    public float dropGravityScale = 1f;
    public float dropLifetime = 5f;
    private bool isPaused = false;
    private Coroutine spawnCoroutine;

    public void startCor()
    {
        if (spawnCoroutine == null) // Запускаем корутину, только если она не запущена
        {
            spawnCoroutine = StartCoroutine(SpawnCoroutine());
        }
    }

    public void stopCor()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    private Coroutine resumeCoroutine;

    public void PauseSpawning()
    {
        // Отменяем отложенное возобновление: раньше пауза, поставленная в течение 7 с
        // после ResumeSpawning, снималась сама.
        if (resumeCoroutine != null)
        {
            StopCoroutine(resumeCoroutine);
            resumeCoroutine = null;
        }
        isPaused = true;
    }

    public void ResumeSpawning()
    {
        if (resumeCoroutine != null) StopCoroutine(resumeCoroutine);
        if (isActiveAndEnabled)
            resumeCoroutine = StartCoroutine(ResumeAfterDelay());
    }

    private IEnumerator ResumeAfterDelay()
    {
        yield return new WaitForSeconds(7f);
        isPaused = false;
        resumeCoroutine = null;
    }

    IEnumerator SpawnCoroutine()
    {
        yield return new WaitForSeconds(_delay);
        while (true)
        {
            if (!isPaused) // Проверка на паузу
            {
                // Точка внутри куба в его локальных координатах — работает при любом масштабе установки (VR: 1:70).
                Vector3 spawnPosition = spawnAreaCube.transform.TransformPoint(new Vector3(
                    Random.Range(-0.5f, 0.5f),
                    Random.Range(-0.5f, 0.5f),
                    Random.Range(-0.5f, 0.5f)
                ));

                GameObject spawnedPrefab = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
                spawnedPrefab.transform.localScale = prefabToSpawn.transform.localScale * dropScale;
                var dropCollider = spawnedPrefab.GetComponent<SphereCollider>();
                if (dropCollider != null) dropCollider.radius *= dropColliderScale;
                var dropBody = spawnedPrefab.GetComponent<Rigidbody>();
                if (dropBody != null && !Mathf.Approximately(dropGravityScale, 1f))
                {
                    dropBody.useGravity = false;
                    spawnedPrefab.AddComponent<ConstantForce>().force = Physics.gravity * dropGravityScale * dropBody.mass;
                }
                spawnedPrefab.transform.SetParent(spawnAreaCube.transform);
                // Страховка: капля, ни во что не попавшая, не копится вечно.
                Destroy(spawnedPrefab, dropLifetime);
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }
}
