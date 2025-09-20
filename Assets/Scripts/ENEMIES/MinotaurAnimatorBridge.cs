using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MinotaurAnimatorBridge : MonoBehaviour
{
    public Animator anim;            // assign in Inspector
    NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!anim) anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // Feed movement speed to Animator (no root motion)
        anim.SetFloat("Speed", agent.velocity.magnitude);
    }

    // Call these from your AI when appropriate:
    public void PlayAttack() => anim.SetTrigger("Attack");
    public void PlayDie() => anim.SetTrigger("Die");
}
