using UnityEngine;

public class Enemy2D : MonoBehaviour
{
    [SerializeField] private Transform hitPoint;
    [SerializeField] private int maxHp = 500;

    private int currentHp;

    public Transform HitPoint => hitPoint;

    private void Awake()
    {
        currentHp = maxHp;
    }

    public void TakeDamage(int damage)
    {
        currentHp -= damage;

        Debug.Log(
            $"{name} 受到 {damage} 点伤害，剩余HP：{currentHp}"
        );

        if (currentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}