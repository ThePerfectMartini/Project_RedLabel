using System.Collections;
using UnityEngine;

public class MoveState : ActionState
{
    private MoveAction action;

    public MoveState(CapsuleController controller, MoveAction actionData) : base(controller, actionData)
    {
        this.action = actionData;
    }

    private Transform GetResolvedTarget()
    {
        if (action.targetType == TargetType.TrackObject)
        {
            GameObject targetGO = GameObject.FindWithTag(action.targetTag);
            if (targetGO != null) return targetGO.transform;
        }
        return null;
    }

    public override IEnumerator Execute()
    {
        PlayAnimation();

        if (action.isTeleport)
        {
            if (action.targetType == TargetType.Direction)
            {
                // 방향 이동에 대해서는 순간이동 생략
            }
            else
            {
                bool isTracking = action.targetType == TargetType.TrackObject;
                Transform trackingTarget = isTracking ? GetResolvedTarget() : null;
                Vector3 specificPos = action.targetPosition;
                
                controller.TeleportToTarget(trackingTarget, specificPos, isTracking, action);
            }
            yield break;
        }

        if (action.useJump)
        {
            controller.Jump(action.jumpForce);
        }

        if (action.targetType == TargetType.Direction)
        {
            Vector3 dir = CapsuleController.GetDirectionFromEnum(action.moveDirection8);
            yield return controller.StartCoroutine(MoveInDirectionRoutine(dir));
        }
        else 
        {
            bool isTracking = action.targetType == TargetType.TrackObject;
            Transform trackingTarget = isTracking ? GetResolvedTarget() : null;
            
            if (isTracking && trackingTarget == null)
            {
                Debug.LogWarning($"Target object with tag '{action.targetTag}' not found.");
                yield break;
            }

            yield return controller.StartCoroutine(MoveToTargetRoutine(trackingTarget, action.targetPosition, isTracking));
        }
    }

    private IEnumerator MoveInDirectionRoutine(Vector3 moveDir)
    {
        float timer = 0f;
        float currentSpeed = action.startSpeed;
        
        while (true)
        {
            if (action.stopOnTimeLimit && timer >= action.timeLimit)
            {
                break;
            }

            if (action.useAcceleration)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, action.speed, action.acceleration * Time.deltaTime);
            }
            else
            {
                currentSpeed = action.speed;
            }

            Vector3 nextPos = controller.transform.position + (moveDir * currentSpeed * Time.deltaTime);
            controller.rb.MovePosition(nextPos);

            if (moveDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                controller.rb.MoveRotation(Quaternion.Slerp(controller.transform.rotation, targetRot, Time.deltaTime * 10f));
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator MoveToTargetRoutine(Transform trackingTarget, Vector3 specificPos, bool isTracking)
    {
        float timer = 0f;
        float currentSpeed = action.startSpeed;
        int refreshCount = 0;
        float refreshTimer = 0f;

        Vector3 initialRawTargetPos = isTracking ? trackingTarget.position : specificPos;
        Vector3 currentDest = CapsuleController.GetOffsetPosition(controller.transform.position, initialRawTargetPos, action.targetOffset, isTracking, action);
        
        while (true)
        {
            if (action.stopOnTimeLimit && timer >= action.timeLimit) break;

            if (action.stopOnDestinationReached)
            {
                float dist = Vector3.Distance(new Vector3(controller.transform.position.x, 0, controller.transform.position.z), new Vector3(currentDest.x, 0, currentDest.z));
                if (dist <= action.destinationStopDistance) break;
            }

            if (action.stopOnTargetDetected && CheckTargetInDetectRange(currentDest)) break;

            if (isTracking && action.usePositionRefresh)
            {
                refreshTimer += Time.deltaTime;
                if (refreshTimer >= action.positionRefreshInterval)
                {
                    bool canRefresh = action.repeatRefreshUntilReached || (refreshCount < action.refreshRepeatCount);
                    if (canRefresh)
                    {
                        refreshTimer = 0f;
                        refreshCount++;
                        Vector3 rawTargetPos = trackingTarget.position;
                        currentDest = CapsuleController.GetOffsetPosition(controller.transform.position, rawTargetPos, action.targetOffset, isTracking, action);
                    }
                }
            }

            if (action.useAcceleration)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, action.speed, action.acceleration * Time.deltaTime);
            }
            else
            {
                currentSpeed = action.speed;
            }

            Vector3 moveDir = (currentDest - controller.transform.position);
            moveDir.y = 0f;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                moveDir.Normalize();
                Vector3 nextPos = controller.transform.position + (moveDir * currentSpeed * Time.deltaTime);
                controller.rb.MovePosition(nextPos);

                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                controller.rb.MoveRotation(Quaternion.Slerp(controller.transform.rotation, targetRot, Time.deltaTime * 10f));
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private bool CheckTargetInDetectRange(Vector3 currentDest)
    {
        Vector3 originPos = (action.detectOrigin == DetectOrigin.Self) ? controller.transform.position : currentDest;
        Vector3 center = originPos + action.detectOffset;

        Collider[] hits;
        if (action.detectShape == DetectShape.Sphere)
        {
            hits = Physics.OverlapSphere(center, action.detectRadius);
        }
        else
        {
            hits = Physics.OverlapBox(center, action.detectBoxSize * 0.5f);
        }

        foreach (Collider hit in hits)
        {
            if (hit.gameObject != controller.gameObject && hit.CompareTag(action.targetTag))
            {
                return true;
            }
        }
        return false;
    }
}
