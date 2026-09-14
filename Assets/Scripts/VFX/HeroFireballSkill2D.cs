using UnityEngine;
using System;

public class HeroFireballSkill2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private FireballProjectile2D fireballPrefab;
    [SerializeField] private ExplosionEffect2D explosionPrefab;

    [Header("Combat")]
    [SerializeField] private int damage = 100;

    [SerializeField] private Enemy2D testTarget;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            CastSkill(testTarget);
        }
    }

    public void CastSkill(Enemy2D target)
    {
        if (target == null)
            return;

        Transform hitPoint = target.HitPoint;

        Vector3 startPosition =
            firePoint != null
            ? firePoint.position
            : transform.position;

        Vector3 endPosition =
            hitPoint != null
            ? hitPoint.position
            : target.transform.position;

        FireballProjectile2D projectile =
            Instantiate(
                fireballPrefab,
                startPosition,
                Quaternion.identity
            );

        projectile.Launch(
            startPosition,
            endPosition,
            hitPosition =>
            {
                SpawnExplosion(
                    hitPosition,
                    target
                );
            }
        );
    }

    private void SpawnExplosion(
        Vector3 position,
        Enemy2D target)
    {
        ExplosionEffect2D explosion =
            Instantiate(
                explosionPrefab,
                position,
                Quaternion.identity
            );

        explosion.Initialize(
            () =>
            {
                if (target != null)
                {
                    target.TakeDamage(damage);
                }
            }
        );
    }
}
