using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMelee : MonoBehaviour
{
    [Header("Hitbox")]
    public Transform attackPoint;
    public float attackRadius = 0.9f;
    public LayerMask targetMask;

    [Header("Damage / Timing (no animation events)")]
    public float attack1Damage = 18f;
    public float attack2Damage = 28f;
    public float attack1Windup = 0.6f;   // tune to match the animation impact
    public float attack2Windup = 0.9f;

    readonly HashSet<Health> _hitThisSwing = new HashSet<Health>();
    Coroutine swingRoutine;

    public void TriggerAttack1()
    {
        if (swingRoutine != null) StopCoroutine(swingRoutine);
        _hitThisSwing.Clear();
        swingRoutine = StartCoroutine(DoSwingAfter(attack1Windup, attack1Damage));
    }

    public void TriggerAttack2()
    {
        if (swingRoutine != null) StopCoroutine(swingRoutine);
        _hitThisSwing.Clear();
        swingRoutine = StartCoroutine(DoSwingAfter(attack2Windup, attack2Damage));
    }

    IEnumerator DoSwingAfter(float delay, float dmg)
    {
        yield return new WaitForSeconds(delay);
        DoHit(dmg);
        swingRoutine = null;
    }

    void DoHit(float dmg)
    {
        if (!attackPoint) return;

        var hits = Physics.OverlapSphere(
            attackPoint.position,
            attackRadius,
            targetMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (var h in hits)
        {
            var hp = h.GetComponentInParent<Health>();
            if (!hp || _hitThisSwing.Contains(hp)) continue;

            _hitThisSwing.Add(hp);
            hp.Damage(dmg);

            // --- Restore hunger if we hit an enemy ---
            if (h.GetComponentInParent<ChaserEnemyAI>())                // Check for ChaserEnemyAI specifically for now
            {
                var playerHunger = GetComponent<PlayerHunger>();
                if (playerHunger) playerHunger.AddHunger(dmg/5);
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!attackPoint) return;
        Gizmos.color = new Color(1f, .3f, .2f, .5f);
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
#endif
}
