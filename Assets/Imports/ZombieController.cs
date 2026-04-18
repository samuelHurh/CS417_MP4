using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class ZombieController : MonoBehaviour
{
    private NavMeshAgent agent;
    //private Animator animator;
    public Transform target;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        //animator = GetComponent<Animator>();
    }

    void Update()
    {
        //if (animator.GetBool("Dead")) return;

        agent.SetDestination(target.position);

        //animator.SetFloat("Speed", agent.velocity.magnitude);

        if (Vector3.Distance(transform.position, target.position) < 0.8f)
        {
            //animator.SetTrigger("Attack");
            StartCoroutine(TriggerDeath());
        }
    }

    public IEnumerator TriggerDeath()
    {
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene("StartScreen");
    }

    public void SetTarget(Transform tgt)
    {
        target = tgt;
    }

    public void Die()
    {
        //animator.SetBool("Dead", true);
        agent.isStopped = true;
        agent.enabled = false;
        StartCoroutine(Sink());
        Destroy(this.gameObject, 1f);
    }

    public IEnumerator Sink()
    {
        yield return new WaitForSeconds(1f);
        while (true)
        {
            this.transform.position -= new Vector3(0,0.2f, 0);
            yield return new WaitForSeconds(0.02f);
        }
    }
}
