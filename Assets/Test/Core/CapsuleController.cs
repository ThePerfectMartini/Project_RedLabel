using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CapsuleController : MonoBehaviour
{
    [Header("컴포넌트 참조")]
    public AnimationController animController;
    [HideInInspector] public Rigidbody rb;

    [Header("액션 시퀀스 설정")]
    public ActionSequenceSO actionSequence;
    public bool playOnStart = true;

    private Coroutine sequenceCoroutine;
    private int currentActionIndex = 0;
    private bool isSequenceRunning = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (animController == null)
            animController = GetComponentInChildren<AnimationController>();
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
        if (sequenceCoroutine != null) StopCoroutine(sequenceCoroutine);
        sequenceCoroutine = StartCoroutine(ExecuteSequenceCoroutine());
    }

    public void StopActionSequence()
    {
        if (sequenceCoroutine != null) StopCoroutine(sequenceCoroutine);
        isSequenceRunning = false;
        
        if (animController != null && animController.animator != null)
            animController.Play("Idle");
            
        rb.linearVelocity = Vector3.zero;
    }

    private IEnumerator ExecuteSequenceCoroutine()
    {
        if (actionSequence == null || actionSequence.actions == null || actionSequence.actions.Count == 0)
            yield break;

        isSequenceRunning = true;
        int currentRepeatCount = 0;

        while (isSequenceRunning)
        {
            for (int i = 0; i < actionSequence.actions.Count; i++)
            {
                currentActionIndex = i;
                ActionBase action = actionSequence.actions[i];
                if (action == null) continue;

                ActionState newState = action.CreateState(this);

                if (action.executeParallel)
                {
                    StartCoroutine(ExecuteStateCoroutine(action, newState));
                }
                else
                {
                    yield return StartCoroutine(ExecuteStateCoroutine(action, newState));
                }
            }

            currentRepeatCount++;

            if (!actionSequence.isInfiniteLoop && currentRepeatCount >= actionSequence.repeatCount)
            {
                break;
            }
        }

        isSequenceRunning = false;
    }

    private IEnumerator ExecuteStateCoroutine(ActionBase actionData, ActionState state)
    {
        yield return StartCoroutine(state.Execute());
    }

    // --- Action API ---

    public void TeleportToTarget(Transform trackingTarget, Vector3 specificPos, bool isTracking, MoveAction actionData)
    {
        Vector3 dest = GetOffsetPosition(transform.position, isTracking ? trackingTarget.position : specificPos, actionData.targetOffset, isTracking, actionData);
        rb.MovePosition(dest);
        transform.position = dest; // 강제 반영
    }

    public void Jump(float jumpForce)
    {
        Vector3 vel = rb.linearVelocity;
        vel.y = jumpForce;
        rb.linearVelocity = vel;
    }

    // --- Utilities ---

    public static Vector3 GetDirectionFromEnum(MoveDirection8 dir8)
    {
        switch (dir8)
        {
            case MoveDirection8.Up: return Vector3.forward;
            case MoveDirection8.Down: return Vector3.back;
            case MoveDirection8.Left: return Vector3.left;
            case MoveDirection8.Right: return Vector3.right;
            case MoveDirection8.UpLeft: return new Vector3(-1, 0, 1).normalized;
            case MoveDirection8.UpRight: return new Vector3(1, 0, 1).normalized;
            case MoveDirection8.DownLeft: return new Vector3(-1, 0, -1).normalized;
            case MoveDirection8.DownRight: return new Vector3(1, 0, -1).normalized;
            default: return Vector3.zero;
        }
    }

    public static Vector3 GetOffsetPosition(Vector3 myPos, Vector3 targetPos, Vector3 offsetInput, bool isTracking, MoveAction data)
    {
        if (isTracking && data != null)
        {
            if (data.trackXOnly && !data.trackZOnly) targetPos.z = myPos.z;
            else if (!data.trackXOnly && data.trackZOnly) targetPos.x = myPos.x;
        }
        
        Vector3 finalPos = targetPos + offsetInput;
        finalPos.y = myPos.y; 
        return finalPos;
    }

    // --- Gizmos ---

    private void OnDrawGizmos()
    {
        ActionGizmoUtility.DrawGizmos(this, actionSequence, currentActionIndex);
    }
}
