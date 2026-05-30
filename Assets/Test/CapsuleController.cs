using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CapsuleController : MonoBehaviour
{
    public AnimationController animController;

    private Rigidbody rb;
    private Collider col;

    // ── 수동 이동(코루틴에 의한 위치 이동) 진행 상태 트래킹 ──
    private bool isManualMoving;
    public bool IsManualMoving => isManualMoving;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rb.freezeRotation = true; 
        
        if (animController == null)
        {
            animController = GetComponentInChildren<AnimationController>();
        }
    }

    private void UpdateFacingDirection(float xDir)
    {
        if (Mathf.Abs(xDir) > 0.01f)
        {
            float yRotation = xDir > 0 ? 180f : 0f;
            transform.rotation = Quaternion.Euler(0, yRotation, 0);
        }
    }

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
            case MoveDirection8.Forward: return Vector3.left; // 2.5D 벨트스크롤 물리 기준 캐릭터의 앞 방향
            case MoveDirection8.Backward: return Vector3.right; // 2.5D 벨트스크롤 물리 기준 캐릭터의 뒤 방향
            default: return Vector3.zero;
        }
    }

    public void Jump(float jumpForce)
    {
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private Vector3 Get8Direction(Vector3 dir)
    {
        Vector3 flatDir = new Vector3(dir.x, 0, dir.z);
        if (flatDir.sqrMagnitude < 0.001f) return dir.normalized; 

        float angle = Mathf.Atan2(flatDir.z, flatDir.x) * Mathf.Rad2Deg;
        float snappedAngle = Mathf.Round(angle / 45f) * 45f;
        float rad = snappedAngle * Mathf.Deg2Rad;

        Vector3 snappedDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
        snappedDir.y = dir.normalized.y; 

        return snappedDir.normalized;
    }

    public static float ApplyEaseCurve(float t, EaseType easeType, float exponent)
    {
        t = Mathf.Clamp01(t);
        if (easeType == EaseType.EaseIn)
        {
            return Mathf.Pow(t, exponent);
        }
        else // EaseOut
        {
            return 1f - Mathf.Pow(1f - t, exponent);
        }
    }

    public IEnumerator MoveInDirection(Vector3 direction, MoveActionData actionData, InterruptToken token = null)
    {
        isManualMoving = true;
        try
        {
            float maxSpeed = Mathf.Max(0.01f, actionData.speed);
            float startSpeed = Mathf.Max(0f, actionData.startSpeed);
            float startExponent = Mathf.Max(1f, actionData.startEaseExponent);
            float startEaseDuration = Mathf.Max(0f, actionData.startEaseDuration);

            float elapsed = 0f;
            Vector3 normalizedDir = actionData.use8DirectionMovement ? Get8Direction(direction.normalized) : direction.normalized;
            UpdateFacingDirection(normalizedDir.x);

            while (true)
            {
                if (token != null && token.IsInterrupted) yield break;

                float currentSpeed = maxSpeed;
                elapsed += Time.fixedDeltaTime;

                // 출발 가속 연산 (시간 기준)
                if (actionData.useStartEase && startEaseDuration > 0.001f && elapsed < startEaseDuration)
                {
                    float t = Mathf.Clamp01(elapsed / startEaseDuration);
                    float tVal = ApplyEaseCurve(t, actionData.startEaseType, startExponent);
                    currentSpeed = Mathf.Lerp(startSpeed, maxSpeed, tVal);
                }

                rb.linearVelocity = new Vector3(normalizedDir.x * currentSpeed, rb.linearVelocity.y, normalizedDir.z * currentSpeed);

                if (actionData.stopOnTimeLimit)
                {
                    if (elapsed >= actionData.timeLimit) yield break;
                }

                yield return new WaitForFixedUpdate();
            }
        }
        finally
        {
            if (!actionData.allowSlideAfterAction)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
            isManualMoving = false;
        }
    }

    public Vector3 GetOffsetPosition(Vector3 currentPos, Vector3 targetPos, Vector3 offset)
    {
        float signX = (targetPos.x == currentPos.x) ? 0f : Mathf.Sign(targetPos.x - currentPos.x);
        float signZ = (targetPos.z == currentPos.z) ? 0f : Mathf.Sign(targetPos.z - currentPos.z);
        
        return new Vector3(
            targetPos.x - (signX * offset.x),
            targetPos.y, // Y는 항상 0으로 고정됨 (호출 전 설정)
            targetPos.z - (signZ * offset.z)
        );
    }

    public IEnumerator MoveToTarget(Transform trackingTarget, Vector3 specificTargetPos, bool isTracking, MoveActionData actionData,
                                     InterruptToken token = null)
    {
        isManualMoving = true;
        try
        {
            float maxSpeed = Mathf.Max(0.01f, actionData.speed);
            float startSpeed = Mathf.Max(0f, actionData.startSpeed);
            float startExponent = Mathf.Max(1f, actionData.startEaseExponent);
            float startEaseDuration = Mathf.Max(0f, actionData.startEaseDuration);

            float totalTimer = 0f;
            float refreshTimer = 0f;
            int refreshCount = 0;

            bool checkDest = actionData.stopOnDestinationReached;
            bool checkDetect = actionData.stopOnTargetDetected && isTracking;
            bool checkTime = actionData.stopOnTimeLimit;

            bool canRefresh = actionData.usePositionRefresh;

            Vector3 initialTargetPos = isTracking ? trackingTarget.position : specificTargetPos;
            if (actionData.trackXOnly && !actionData.trackZOnly) initialTargetPos.z = rb.position.z;
            else if (!actionData.trackXOnly && actionData.trackZOnly) initialTargetPos.x = rb.position.x;
            initialTargetPos.y = 0f;

            Vector3 startPos = rb.position;
            startPos.y = 0f;
            Vector3 initialOffsetTargetPos = GetOffsetPosition(startPos, initialTargetPos, actionData.targetOffset);

            while (true)
            {
                Vector3 baseTargetPos = specificTargetPos;
                if (isTracking)
                {
                    if (trackingTarget == null) yield break; 
                    baseTargetPos = trackingTarget.position;
                }

                if (actionData.trackXOnly && !actionData.trackZOnly) baseTargetPos.z = rb.position.z;
                else if (!actionData.trackXOnly && actionData.trackZOnly) baseTargetPos.x = rb.position.x;

                baseTargetPos.y = 0f;

                Vector3 offsetTargetPos = GetOffsetPosition(rb.position, baseTargetPos, actionData.targetOffset);

                bool needsRefresh = false;
                while (!needsRefresh)
                {
                    if (token != null && token.IsInterrupted) yield break;

                    if (checkTime)
                    {
                        totalTimer += Time.fixedDeltaTime;
                        if (totalTimer >= actionData.timeLimit) yield break;
                    }

                    if (checkDest && CheckDestinationReached(offsetTargetPos, actionData.destinationStopDistance))
                    {
                        yield break;
                    }

                    if (checkDetect && trackingTarget != null && CheckTargetInDetectRange(trackingTarget.position, actionData))
                        yield break;

                    Vector3 destination = offsetTargetPos;

                    Vector3 dirToDest = destination - rb.position;
                    dirToDest.y = 0f; 

                    Vector3 moveDir = actionData.use8DirectionMovement ? Get8Direction(dirToDest) : dirToDest.normalized;
                    UpdateFacingDirection(moveDir.x);

                    float currentSpeed = maxSpeed;

                    // 출발 가속 연산 (시간 기준)
                    if (actionData.useStartEase && startEaseDuration > 0.001f && totalTimer < startEaseDuration)
                    {
                        float t = Mathf.Clamp01(totalTimer / startEaseDuration);
                        float tVal = ApplyEaseCurve(t, actionData.startEaseType, startExponent);
                        currentSpeed = Mathf.Lerp(startSpeed, maxSpeed, tVal);
                    }

                    Vector3 velocity = moveDir * currentSpeed;
                    rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
                    
                    yield return new WaitForFixedUpdate();

                    // 경과 시간 누적
                    totalTimer += Time.fixedDeltaTime;

                    if (canRefresh)
                    {
                        refreshTimer += Time.fixedDeltaTime;
                        if (refreshTimer >= actionData.positionRefreshInterval)
                        {
                            refreshTimer = 0f;
                            refreshCount++;
                            if (!actionData.repeatRefreshUntilReached && refreshCount >= actionData.refreshRepeatCount)
                            {
                                canRefresh = false; 
                            }
                            needsRefresh = true; 
                        }
                    }
                    else if (isTracking && !actionData.usePositionRefresh)
                    {
                        needsRefresh = true;
                    }
                }
            }
        }
        finally
        {
            if (!actionData.allowSlideAfterAction)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
            isManualMoving = false;
        }
    }

    /// <summary>
    /// 종료 조건 1: 절대 좌표 기준으로 목표 좌표에 도달했는지 검사
    /// </summary>
    private bool CheckDestinationReached(Vector3 targetPos, float stopDistance)
    {
        Vector3 currentPos = rb.position;
        targetPos.y = currentPos.y;
        float allowedDist = Mathf.Max(stopDistance, 0.01f);
        return Vector3.Distance(currentPos, targetPos) <= allowedDist;
    }

    /// <summary>
    /// 종료 조건 2: 추적 타겟이 감지 범위 안에 있는지 검사
    /// </summary>
    private bool CheckTargetInDetectRange(Vector3 targetObjPos, MoveActionData actionData)
    {
        targetObjPos.y = rb.position.y;

        if (actionData.detectOrigin == DetectOrigin.Self)
        {
            // 자신 기준: 자신의 로컬 좌표계에서 감지 오프셋 위치를 중심으로 타겟이 범위 안에 있는지
            Vector3 detectCenter = transform.TransformPoint(actionData.detectOffset);
            if (actionData.detectShape == DetectShape.Sphere)
            {
                return Vector3.Distance(detectCenter, targetObjPos) <= actionData.detectRadius;
            }
            else // Box
            {
                Vector3 localTarget = transform.InverseTransformPoint(targetObjPos) - actionData.detectOffset;
                localTarget.y = 0f;
                bool inX = Mathf.Abs(localTarget.x) <= actionData.detectBoxSize.x * 0.5f;
                bool inY = true;
                bool inZ = Mathf.Abs(localTarget.z) <= actionData.detectBoxSize.z * 0.5f;
                return inX && inY && inZ;
            }
        }
        else // DetectOrigin.Target
        {
            // 타겟 기준: 타겟 위치 + 오프셋을 중심으로 자신이 범위 안에 있는지
            Vector3 detectCenter = targetObjPos + actionData.detectOffset;
            if (actionData.detectShape == DetectShape.Sphere)
            {
                Vector3 closestPoint = col.ClosestPoint(detectCenter);
                closestPoint.y = rb.position.y;
                return Vector3.Distance(closestPoint, detectCenter) <= actionData.detectRadius;
            }
            else // Box
            {
                Vector3 diff = rb.position - detectCenter;
                diff.y = 0f;
                bool inX = Mathf.Abs(diff.x) <= actionData.detectBoxSize.x * 0.5f;
                bool inY = true;
                bool inZ = Mathf.Abs(diff.z) <= actionData.detectBoxSize.z * 0.5f;
                return inX && inY && inZ;
            }
        }
    }

    public void TeleportToTarget(Transform trackingTarget, Vector3 specificTargetPos, bool isTracking, MoveActionData actionData)
    {
        Vector3 baseTargetPos = specificTargetPos;
        if (isTracking)
        {
            if (trackingTarget == null) return;
            baseTargetPos = trackingTarget.position;
        }

        if (actionData.trackXOnly && !actionData.trackZOnly) baseTargetPos.z = rb.position.z;
        else if (!actionData.trackXOnly && actionData.trackZOnly) baseTargetPos.x = rb.position.x;

        baseTargetPos.y = 0f;

        Vector3 offsetTargetPos = GetOffsetPosition(rb.position, baseTargetPos, actionData.targetOffset);

        UpdateFacingDirection((offsetTargetPos - rb.position).x);

        rb.linearVelocity = Vector3.zero; 
        rb.position = offsetTargetPos;
    }
}