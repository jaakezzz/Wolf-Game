using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    public float maxHP = 100f;
    public float currentHP;
    public UnityEvent onDeath;
    public UnityEvent onHurt;
    public UnityEvent<float, float> onHealthChanged; // current, max

    bool dead;

    void Awake()
    {
        currentHP = maxHP;
        dead = false;
        onHealthChanged?.Invoke(currentHP, maxHP);
    }

    public void Damage(float dmg)
    {
        if (dead) return;                               // <— ignore after death
        currentHP = Mathf.Max(0f, currentHP - dmg);
        onHealthChanged?.Invoke(currentHP, maxHP);
        if (dmg > 0f && currentHP > 0f)   // still alive after damage
            onHurt?.Invoke();
        if (currentHP <= 0f) Die();
    }

    public void Heal(float amt)
    {
        if (dead) return;                               // block heals when dead
        currentHP = Mathf.Min(maxHP, currentHP + amt);
        onHealthChanged?.Invoke(currentHP, maxHP);
    }

    void Die()
    {
        if (dead) return;                               // <— latch
        dead = true;
        onDeath?.Invoke();
    }

    // call this on respawn
    public void Revive()
    {
        dead = false;
        currentHP = maxHP;
        onHealthChanged?.Invoke(currentHP, maxHP);
    }
}
