using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class FireballProjectile2D : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] private float flightDuration = 0.45f;
    [SerializeField] private float arcHeight = 0.7f;

    [Header("Visual")]
    [SerializeField] private float rotateSpeed = 120f;
    [SerializeField] private float startScale = 0.9f;
    [SerializeField] private float peakScale = 1.15f;
    [SerializeField] private float endScale = 0.9f;

    private Vector3 startPosition;
    private Vector3 endPosition;

    private float timer;
    private bool isFlying;
    private Action<Vector3> onArrive;

    private Vector3 baseScale;

    public void Launch(
        Vector3 start,
        Vector3 end,
        Action<Vector3> arriveCallback)
    {
        startPosition = start;
        endPosition = end;

        transform.position = startPosition;

        timer = 0f;
        isFlying = true;

        onArrive = arriveCallback;

        baseScale = transform.localScale;
    }

    private void Update()
    {
        if (!isFlying)
            return;

        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / flightDuration);

        // 1. 起点到终点的基础移动
        Vector3 position =
            Vector3.Lerp(startPosition, endPosition, t);

        // 2. 添加弧线
        float arc =
            Mathf.Sin(t * Mathf.PI) * arcHeight;

        position.y += arc;

        transform.position = position;

        // 3. 火球旋转
        transform.Rotate(
            0f,
            0f,
            -rotateSpeed * Time.deltaTime
        );

        // 4. 模拟飞高时变大
        float scaleMultiplier;

        if (t <= 0.5f)
        {
            scaleMultiplier =
                Mathf.Lerp(
                    startScale,
                    peakScale,
                    t / 0.5f
                );
        }
        else
        {
            scaleMultiplier =
                Mathf.Lerp(
                    peakScale,
                    endScale,
                    (t - 0.5f) / 0.5f
                );
        }

        transform.localScale =
            baseScale * scaleMultiplier;

        // 5. 到达终点
        if (t >= 1f)
        {
            Arrive();
        }
    }

    private void Arrive()
    {
        if (!isFlying)
            return;

        isFlying = false;

        onArrive?.Invoke(endPosition);

        Destroy(gameObject);
    }
}
