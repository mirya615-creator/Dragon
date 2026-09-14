using System;
using UnityEngine;

public class ExplosionEffect2D : MonoBehaviour
{
    private Action damageCallback;

    public void Initialize(Action onDamage)
    {
        damageCallback = onDamage;
    }

    // 在爆炸最大帧调用
    public void OnDamageFrame()
    {
        damageCallback?.Invoke();
    }

    // 动画最后一帧调用
    public void OnAnimationEnd()
    {
        Destroy(gameObject);
    }
}
