using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CapsuleController : MonoBehaviour
{
    [Header("행동 패턴 설정")]
    [SerializeField] private ActionSequenceSO actionSequence; 
    [SerializeField] private bool playOnStart = true;        

    private Rigidbody rb;
    private Collider col;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rb.freezeRotation = true; 
    }

    private void Start()
    {
        if (playOnStart && actionSequence != null)
        {
            StartActionSequence();
        }
    }

    public void StartActionSequence()
    {
        if (actionSequence != null)
        {
            StartCoroutine(actionSequence.ExecuteSequence(this));
        }
    }

    public IEnumerator MoveToPosition(Vector3 targetPos, float startSpeed, float maxSpeed, bool isAccelerated, float accel, bool checkTarget, float stopDist, bool checkTime, float timeLimit)
    {
        maxSpeed = Mathf.Max(0.01f, maxSpeed);
        startSpeed = Mathf.Max(0f, startSpeed);
        if (isAccelerated) accel = Mathf.Max(0.01f, accel);

        float currentSpeed = isAccelerated ? startSpeed : maxSpeed;
        float timer = 0f;

        while (true)
        {
            if (checkTime)
            {
                timer += Time.fixedDeltaTime; 
                if (timer >= timeLimit) yield break;
            }

            Vector3 closestPoint = col.ClosestPoint(targetPos);
            float distance = Vector3.Distance(closestPoint, targetPos);
            
            if (checkTarget && distance <= stopDist) yield break;

            if (isAccelerated)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, accel * Time.fixedDeltaTime);
            }

            Vector3 nextPos = Vector3.MoveTowards(rb.position, targetPos, currentSpeed * Time.fixedDeltaTime);
            rb.MovePosition(nextPos);
            
            yield return new WaitForFixedUpdate();
        }
    }

    public IEnumerator TrackObject(Transform target, Vector3 offset, float startSpeed, float maxSpeed, bool isAccelerated, float accel, bool checkTarget, float stopDist, bool checkTime, float timeLimit)
    {
        maxSpeed = Mathf.Max(0.01f, maxSpeed);
        startSpeed = Mathf.Max(0f, startSpeed);
        if (isAccelerated) accel = Mathf.Max(0.01f, accel);

        float currentSpeed = isAccelerated ? startSpeed : maxSpeed;
        float timer = 0f;

        while (target != null)
        {
            if (checkTime)
            {
                timer += Time.fixedDeltaTime;
                if (timer >= timeLimit) yield break;
            }

            Vector3 targetPos = target.position + offset;
            Vector3 closestPoint = col.ClosestPoint(targetPos);
            float distance = Vector3.Distance(closestPoint, targetPos);
            
            if (checkTarget && distance <= stopDist) yield break;

            if (isAccelerated)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, accel * Time.fixedDeltaTime);
            }

            Vector3 nextPos = Vector3.MoveTowards(rb.position, targetPos, currentSpeed * Time.fixedDeltaTime);
            rb.MovePosition(nextPos);
            
            yield return new WaitForFixedUpdate();
        }
    }

    public void TeleportToPosition(Vector3 targetPos)
    {
        rb.linearVelocity = Vector3.zero; 
        rb.position = targetPos; 
    }

    public void TeleportToObject(Transform target, Vector3 offset)
    {
        if (target != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.position = target.position + offset;
        }
    }

    private void OnDrawGizmos()
    {
        if (actionSequence == null || actionSequence.actions == null) return;

        foreach (var action in actionSequence.actions)
        {
            if (!action.showGizmo) continue;

            Vector3 finalTargetPos = action.targetPosition;

            if (action.targetType == TargetType.TrackObject)
            {
                GameObject targetGO = GameObject.FindWithTag(action.targetTag);
                if (targetGO != null) finalTargetPos = targetGO.transform.position;
            }
            
            finalTargetPos += action.offset;

            Gizmos.color = action.actionType == ActionType.Teleport ? Color.cyan : Color.yellow;
            Gizmos.DrawSphere(finalTargetPos, 0.2f);

            if (action.actionType == ActionType.Move && action.stopOnTargetReached)
            {
                Gizmos.DrawWireSphere(finalTargetPos, action.stopDistance);
            }

            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.3f);
            Gizmos.DrawLine(transform.position, finalTargetPos);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(finalTargetPos + Vector3.up * 0.5f, $"목표: {action.actionType}");
#endif
        }
    }
}