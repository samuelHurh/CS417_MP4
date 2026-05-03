using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class ShooterRoleBrain : EnemyRoleBrain
{
    [SerializeField] private Transform eyePoint;
    [SerializeField] private LayerMask lineOfSightMask = Physics.DefaultRaycastLayers;
    [SerializeField] private float minDist;
    [SerializeField] private float maxDist;

    private bool canSeePlayer;
    public float shotInterval = 0.5f;

    private int maxCandidateChecks = 10;

    private Vector3 currMoveTarget;
    [SerializeField] private float minRepositionRadius = 1f;
    [SerializeField] private float maxRepositionRadius = 2f;
    [SerializeField] private float repositionArrivalDistance = 0.5f;
    [SerializeField] private float repositionTimeInterval = 2f;
    private bool isRepositioning;

    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform bulletSpawn;

    [SerializeField] private float bulletVelocity;

    void Start()
    {
        canSeePlayer = false;
    }

    public override void Tick()
    {
        if (controller == null)
        {
            return;
        }

        if (controller.PlayerTarget == null)
        {
            canSeePlayer = false;
            isRepositioning = false;
            controller.ChangeActionState(EnemyActionState.Idle);
            controller.StopMoving();
            return;
        }

        if (isRepositioning)
        {
            controller.ChangeActionState(EnemyActionState.Reposition);

            if (HasReachedRepositionTarget())
            {
                isRepositioning = false;
                controller.StopMoving();
                return;
            }
            else
            {
                controller.MoveTo(currMoveTarget);
                return;
            }
        }

        if (HasLineOfSightToPlayer() && Vector3.Distance(this.transform.position, controller.PlayerTarget.position) < maxDist)
        {
            if (canSeePlayer == false)
            {
                canSeePlayer = true;
                StartCoroutine(AttackingCoroutine());
                StartCoroutine(RepositionCoroutine());
            }
            controller.ChangeActionState(EnemyActionState.Attack);
            controller.StopMoving();
            FacePlayer();
            return;
        } else
        {
            canSeePlayer = false;
        }
        controller.ChangeActionState(EnemyActionState.MoveToPlayer);
        controller.MoveTo(controller.PlayerTarget.position);
    }

    private bool HasLineOfSightToPlayer()
    {
        Vector3 origin = eyePoint != null ? eyePoint.position : transform.position + Vector3.up;
        Vector3 toPlayer = controller.PlayerTarget.position - origin;

        if (!Physics.Raycast(origin, toPlayer.normalized, out RaycastHit hit, toPlayer.magnitude, lineOfSightMask))
        {
            return true;
        }

        return IsHitOnPlayer(hit.transform);
    }

    private bool IsHitOnPlayer(Transform hitTransform)
    {
        Transform playerTarget = controller.PlayerTarget;
        return hitTransform == playerTarget ||
               hitTransform.IsChildOf(playerTarget) ||
               playerTarget.IsChildOf(hitTransform) ||
               hitTransform.root == playerTarget.root;
    }

    private void Fire()
    {
        Vector3 origin = eyePoint != null ? eyePoint.position : transform.position + Vector3.up;
        //Debug.DrawLine(origin, controller.PlayerTarget.position, Color.yellow, 0.1f);
        GameObject newBullet = Instantiate(bulletPrefab, bulletSpawn.position, bulletSpawn.rotation);
        newBullet.GetComponent<Rigidbody>().AddForce(newBullet.transform.forward * bulletVelocity, ForceMode. Impulse);
        Destroy(newBullet, 5f);
    }

    private void FacePlayer()
    {
        Vector3 toPlayer = controller.PlayerTarget.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude <= 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(toPlayer);
    }

    public IEnumerator AttackingCoroutine()
    {
        while (canSeePlayer)
        {
            yield return new WaitForSeconds(shotInterval);
            Fire();
        }
    }

    public IEnumerator RepositionCoroutine()
    {
        yield return new WaitForSeconds(repositionTimeInterval);
        while (canSeePlayer)
        {
            yield return new WaitForSeconds(repositionTimeInterval);

            if (PickReposition(out Vector3 repositionTarget))
            {
                currMoveTarget = repositionTarget;
                isRepositioning = true;
                canSeePlayer = false;
                controller.ChangeActionState(EnemyActionState.Reposition);
            }
        }
    }

    private bool HasReachedRepositionTarget()
    {
        Vector3 currentPosition = transform.position;
        Vector3 targetPosition = currMoveTarget;

        currentPosition.y = 0f;
        targetPosition.y = 0f;

        return Vector3.Distance(currentPosition, targetPosition) <= repositionArrivalDistance;
    }

    public bool PickReposition(out Vector3 chosenPos)
    {
        bool isValidReposition = false;
        chosenPos = this.transform.position;

        for (int i = 0; i < maxCandidateChecks; i++)
        {
            float candidateAngle = Random.Range(0,360f);
            float candidateRadius = Random.Range(minRepositionRadius, maxRepositionRadius);
            //Spherecast for validity. IF true set isValidReposition to true
            Vector3 candidateDirection = Quaternion.Euler(0f, candidateAngle, 0f) * Vector3.forward;
            Vector3 candidatePosition = transform.position + candidateDirection * candidateRadius;

            //validity check
            //navmesh check
            Vector3 navHitPosition;
            if (controller.TryGetNearestNavMeshPosition(candidatePosition, 1f, out navHitPosition) == false)
            {
                //Debug.Log("Failing nav hit check");
                continue;
            }

            //min max dist respect
            if (Vector3.Distance(transform.position, navHitPosition) <= repositionArrivalDistance)
            {
                //Debug.Log("Failing too-close reposition check");
                continue;
            }

            float distanceToPlayer = Vector3.Distance(navHitPosition, controller.PlayerTarget.position);

            if (distanceToPlayer < minDist || distanceToPlayer > maxDist)
            {
                //Debug.Log("Failing max_dist respect");
                continue;
            }

            //Pathing check
            NavMeshPath path = new NavMeshPath();

            if (!controller.NavMeshAgent.CalculatePath(navHitPosition, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                Debug.Log("Failing pathing check");
                continue;
            }

            chosenPos = navHitPosition;
            isValidReposition = true;
            break;

        }
        if (isValidReposition)
        {
            return true;
        }

        return false;
    }
}
