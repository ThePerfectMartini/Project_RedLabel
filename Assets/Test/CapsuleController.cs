using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CapsuleController : MonoBehaviour
{
    [Header("행 패턴 설정")]
    [SerializeField] private ActionSequenceSO actionSequence; 
    [SerializeField] private bool playOnStart = true;    
    
    public AnimationController animController;

    private Rigidbody rb;
    private Collider col;

    private ActionState currentState;
    
    private HashSet<ActionData> activeActions = new HashSet<ActionData>();

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
            StartCoroutine(ExecuteSequenceCoroutine());
        }
    }

    private IEnumerator ExecuteSequenceCoroutine()
    {
        int currentRepeat = 0;
        while (actionSequence.isInfiniteLoop || currentRepeat < actionSequence.repeatCount)
        {
            foreach (var action in actionSequence.actions)
            {
                ActionState nextState = CreateState(action);
                
                if (nextState != null)
                {
                    if (action.executeParallel)
                    {
                        StartCoroutine(ExecuteStateCoroutine(action, nextState));
                    }
                    else
                    {
                        yield return StartCoroutine(ExecuteStateCoroutine(action, nextState));
                    }
                }
            }
            currentRepeat++;
        }
    }

    private IEnumerator ExecuteStateCoroutine(ActionData action, ActionState newState)
    {
        activeActions.Add(action);
        currentState = newState;
        
        yield return StartCoroutine(newState.Execute());
        
        activeActions.Remove(action);
    }

    private ActionState CreateState(ActionData action)
    {
        switch (action.actionType)
        {
            case ActionType.Move: return new MoveState(this, action);
            case ActionType.Wait: return new WaitState(this, action);
            case ActionType.VariableAttack: 
            case ActionType.FixedAttack: return new AttackState(this, action);
            case ActionType.RangedAttack: return new RangedAttackState(this, action); 
            default: return null;
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

    public IEnumerator MoveInDirection(Vector3 direction, float startSpeed, float maxSpeed, bool isAccelerated, float accel, bool checkTime, float timeLimit)
    {
        maxSpeed = Mathf.Max(0.01f, maxSpeed);
        startSpeed = Mathf.Max(0f, startSpeed);
        if (isAccelerated) accel = Mathf.Max(0.01f, accel);

        float currentSpeed = isAccelerated ? startSpeed : maxSpeed;
        float timer = 0f;
        
        Vector3 normalizedDir = Get8Direction(direction.normalized);
        UpdateFacingDirection(normalizedDir.x);

        while (true)
        {
            if (checkTime)
            {
                timer += Time.fixedDeltaTime; 
                if (timer >= timeLimit) yield break;
            }

            if (isAccelerated)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, accel * Time.fixedDeltaTime);
            }

            Vector3 nextPos = rb.position + (normalizedDir * currentSpeed * Time.fixedDeltaTime);
            // 방향 이동에서도 y축은 변하지 않도록 고정
            nextPos.y = rb.position.y;
            rb.MovePosition(nextPos);
            
            yield return new WaitForFixedUpdate();
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

    public IEnumerator MoveToTarget(Transform trackingTarget, Vector3 specificTargetPos, bool isTracking, ActionData actionData)
    {
        float maxSpeed = Mathf.Max(0.01f, actionData.speed);
        float startSpeed = Mathf.Max(0f, actionData.startSpeed);
        float accel = actionData.useAcceleration ? Mathf.Max(0.01f, actionData.acceleration) : 0f;

        float currentSpeed = actionData.useAcceleration ? startSpeed : maxSpeed;
        float totalTimer = 0f;
        float refreshTimer = 0f;
        int refreshCount = 0;

        bool checkDest = actionData.stopOnDestinationReached;
        bool checkDetect = actionData.stopOnTargetDetected && isTracking;
        bool checkTime = actionData.stopOnTimeLimit;

        bool canRefresh = actionData.usePositionRefresh;

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
                if (checkTime)
                {
                    totalTimer += Time.fixedDeltaTime;
                    if (totalTimer >= actionData.timeLimit) yield break;
                }

                // 종료 조건 1: 목표 좌표 도달 검사 (절대 좌표 기준)
                if (checkDest && CheckDestinationReached(offsetTargetPos, actionData.destinationStopDistance))
                    yield break;

                // 종료 조건 2: 이동 중 타겟 감지 검사 (추적 모드에서만)
                if (checkDetect && trackingTarget != null && CheckTargetInDetectRange(trackingTarget.position, actionData))
                    yield break;

                Vector3 destination = offsetTargetPos;

                Vector3 dirToDest = destination - rb.position;
                dirToDest.y = 0f; 
                float distToDest = dirToDest.magnitude;

                Vector3 moveDir = Get8Direction(dirToDest);
                UpdateFacingDirection(moveDir.x);

                if (actionData.useAcceleration)
                {
                    currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, accel * Time.fixedDeltaTime);
                }

                float moveStep = currentSpeed * Time.fixedDeltaTime;
                Vector3 nextPos;

                if (distToDest <= moveStep)
                {
                    nextPos = rb.position + dirToDest;
                }
                else
                {
                    nextPos = rb.position + (moveDir * moveStep);
                }

                rb.MovePosition(nextPos);
                
                yield return new WaitForFixedUpdate();

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
                    // 동적 새로고침 미사용 시에만 매 프레임 추적 갱신
                    // 동적 새로고침 사용 시 횟수 소진 후에는 마지막 좌표에 고정
                    needsRefresh = true;
                }
            }
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
    private bool CheckTargetInDetectRange(Vector3 targetObjPos, ActionData actionData)
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

    public void TeleportToTarget(Transform trackingTarget, Vector3 specificTargetPos, bool isTracking, ActionData actionData)
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

    private void OnDrawGizmos()
    {
        if (actionSequence == null || actionSequence.actions == null) return;

        foreach (var action in actionSequence.actions)
        {
            if (!action.showGizmo) continue;

            if (action.actionType == ActionType.RangedAttack)
            {
                if (Application.isPlaying) continue;

                Gizmos.color = Color.cyan;
                Vector3 spawnPos = transform.position + (transform.rotation * action.attackOffset);
                Gizmos.DrawWireSphere(spawnPos, 0.2f);
                
                Vector3 targetDir = transform.forward;
                
                if (action.targetType == TargetType.TrackObject)
                {
                    if (!string.IsNullOrEmpty(action.targetTag))
                    {
                        GameObject t = GameObject.FindWithTag(action.targetTag);
                        if (t != null) targetDir = (t.transform.position - spawnPos).normalized;
                    }
                }
                else if (action.targetType == TargetType.SpecificPosition)
                {
                    Vector3 tPos = action.targetPosition;
                    targetDir = (tPos - spawnPos).normalized;
                }
                
                if (targetDir == Vector3.zero) targetDir = transform.forward;
                
                DrawArrow(spawnPos, targetDir * 2f);
                continue;
            }

            if (action.actionType == ActionType.VariableAttack || action.actionType == ActionType.FixedAttack)
            {
                if (Application.isPlaying) continue;

                Gizmos.color = new Color(1f, 0f, 0f, 0.4f); 
                
                if (action.actionType == ActionType.FixedAttack)
                {
                    Vector3 basePos = transform.position;
                    
                    if (action.targetType == TargetType.TrackObject)
                    {
                        if (!string.IsNullOrEmpty(action.targetTag))
                        {
                            GameObject tGO = GameObject.FindWithTag(action.targetTag);
                            if (tGO != null) basePos = tGO.transform.position;
                        }
                    }
                    else if (action.targetType == TargetType.SpecificPosition)
                    {
                        basePos = action.targetPosition;
                    }

                    Vector3 attackCenter = basePos + action.attackOffset;
                    Gizmos.matrix = Matrix4x4.identity;

                    if (action.attackShape == AttackShape.Sphere)
                    {
                        Gizmos.DrawWireSphere(attackCenter, action.attackRadius);
                        Gizmos.color = Color.red;
                        Gizmos.DrawSphere(attackCenter, action.attackRadius * 0.1f); 
                    }
                    else if (action.attackShape == AttackShape.Box)
                    {
                        Gizmos.DrawWireCube(attackCenter, action.attackHitBoxSize);
                        Gizmos.color = new Color(1f, 0f, 0f, 0.1f); 
                        Gizmos.DrawCube(attackCenter, action.attackHitBoxSize);
                        Gizmos.color = Color.red;
                        Gizmos.DrawCube(attackCenter, action.attackHitBoxSize * 0.05f); 
                    }
                    else if (action.attackShape == AttackShape.Cylinder)
                    {
                        DrawWireCylinder(attackCenter, action.attackRadius, action.attackHeight);
                        Gizmos.color = Color.red;
                        Gizmos.DrawSphere(attackCenter, action.attackRadius * 0.1f); 
                    }
                }
                else
                {
                    Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                    Gizmos.matrix = rotationMatrix;
                    
                    Vector3 attackCenter = action.attackOffset;

                    if (action.attackShape == AttackShape.Sphere)
                    {
                        Gizmos.DrawWireSphere(attackCenter, action.attackRadius);
                        Gizmos.color = Color.red;
                        Gizmos.DrawSphere(attackCenter, action.attackRadius * 0.1f); 
                    }
                    else if (action.attackShape == AttackShape.Box)
                    {
                        Gizmos.DrawWireCube(attackCenter, action.attackHitBoxSize);
                        Gizmos.color = new Color(1f, 0f, 0f, 0.1f); 
                        Gizmos.DrawCube(attackCenter, action.attackHitBoxSize);
                        Gizmos.color = Color.red;
                        Gizmos.DrawCube(attackCenter, action.attackHitBoxSize * 0.05f); 
                    }
                    else if (action.attackShape == AttackShape.Cylinder)
                    {
                        DrawWireCylinder(attackCenter, action.attackRadius, action.attackHeight);
                        Gizmos.color = Color.red;
                        Gizmos.DrawSphere(attackCenter, action.attackRadius * 0.1f); 
                    }
                }

                Gizmos.matrix = Matrix4x4.identity;
                continue; 
            }

            if (action.targetType == TargetType.Direction)
            {
                Gizmos.color = Color.magenta;
                Vector3 dir = GetDirectionFromEnum(action.moveDirection8);
                Gizmos.DrawRay(transform.position, dir * 3f);
                Gizmos.DrawSphere(transform.position + dir * 3f, 0.15f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(transform.position + dir * 3f + Vector3.up * 0.5f, $"방향 이동\n({action.moveDirection8})");
#endif
                continue;
            }

            Vector3 finalTargetPos = action.targetPosition;
            bool isTracking = action.targetType == TargetType.TrackObject;
            Vector3 rawTargetPos = finalTargetPos; // 추적 원본 좌표 (오프셋 미적용)

            if (isTracking)
            {
                if (!string.IsNullOrEmpty(action.targetTag))
                {
                    GameObject targetGO = GameObject.FindWithTag(action.targetTag);
                    if (targetGO != null) 
                    {
                        finalTargetPos = targetGO.transform.position;
                        rawTargetPos = finalTargetPos;
                    }
                }
            }


            if (action.trackXOnly && !action.trackZOnly) finalTargetPos.z = transform.position.z;
            else if (!action.trackXOnly && action.trackZOnly) finalTargetPos.x = transform.position.x;

            finalTargetPos.y = 0f;

            Vector3 offsetTargetPos = GetOffsetPosition(transform.position, finalTargetPos, action.targetOffset);
            Vector3 destination = offsetTargetPos;

            // ── 기즈모 1: 추적 원본 좌표 (주황색 점) ──
            if (isTracking)
            {
                Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f); // 주황색
                Gizmos.DrawSphere(rawTargetPos, 0.12f);
                // 원본 좌표 → 최종 목적지 점선 연결
                Gizmos.color = new Color(1f, 0.6f, 0f, 0.3f);
                DrawDashedLine(rawTargetPos, destination, 0.3f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(rawTargetPos + Vector3.up * 0.3f, "추적 원본");
#endif
            }

            // ── 기즈모 2: 최종 목적지 (노랑/시안 점) ──
            Gizmos.color = (action.actionType == ActionType.Move && action.isTeleport) ? Color.cyan : Color.yellow;
            Gizmos.DrawSphere(destination, 0.1f);
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.3f);
            Gizmos.DrawLine(transform.position, destination);

            // ── 기즈모 3: 목표 좌표 도달 범위 (초록색 원) ──
            if (action.actionType == ActionType.Move && action.stopOnDestinationReached)
            {
                Gizmos.color = Color.green;
                float drawDist = Mathf.Max(action.destinationStopDistance, 0.01f);
                Gizmos.DrawWireSphere(destination, drawDist);
            }

            // ── 기즈모 4: 타겟 감지 범위 (자홍색) ──
            if (action.actionType == ActionType.Move && action.stopOnTargetDetected && isTracking)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 0.4f); // 자홍색 반투명
                
                if (action.detectOrigin == DetectOrigin.Self)
                {
                    Vector3 detectCenter = transform.TransformPoint(action.detectOffset);
                    if (action.detectShape == DetectShape.Sphere)
                    {
                        Gizmos.DrawWireSphere(detectCenter, action.detectRadius);
                    }
                    else
                    {
                        Matrix4x4 rotMat = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                        Gizmos.matrix = rotMat;
                        Gizmos.DrawWireCube(action.detectOffset, action.detectBoxSize);
                        Gizmos.color = new Color(1f, 0f, 1f, 0.1f);
                        Gizmos.DrawCube(action.detectOffset, action.detectBoxSize);
                        Gizmos.matrix = Matrix4x4.identity;
                    }
                }
                else // DetectOrigin.Target
                {
                    Vector3 detectCenter = rawTargetPos + action.detectOffset;
                    if (action.detectShape == DetectShape.Sphere)
                    {
                        Gizmos.DrawWireSphere(detectCenter, action.detectRadius);
                    }
                    else
                    {
                        Gizmos.DrawWireCube(detectCenter, action.detectBoxSize);
                        Gizmos.color = new Color(1f, 0f, 1f, 0.1f);
                        Gizmos.DrawCube(detectCenter, action.detectBoxSize);
                    }
                }
            }

#if UNITY_EDITOR
            string labelText = $"목표: {action.actionType}";
            if (action.stopOnDestinationReached) labelText += "\n[좌표 도달 종료]";
            if (action.stopOnTargetDetected && isTracking) labelText += "\n[타겟 감지 종료]";
            UnityEditor.Handles.Label(destination + Vector3.up * 0.5f, labelText);
#endif
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (actionSequence == null || actionSequence.actions == null) return;

        foreach (var action in actionSequence.actions)
        {
            if (!action.showGizmo || action.actionType != ActionType.Move) continue;

            Vector3 startPos = transform.position;
            Vector3 dir = Vector3.zero;

            if (action.targetType == TargetType.Direction)
            {
                dir = GetDirectionFromEnum(action.moveDirection8);
            }
            else if (action.targetType == TargetType.SpecificPosition)
            {
                Vector3 targetP = action.targetPosition;
                Vector3 offsetP = GetOffsetPosition(startPos, targetP, action.targetOffset);
                dir = offsetP - startPos;
            }
            else 
            {
                if (!string.IsNullOrEmpty(action.targetTag))
                {
                    GameObject targetGO = GameObject.FindWithTag(action.targetTag);
                    if (targetGO != null)
                    {
                        Vector3 targetP = targetGO.transform.position;
                        if (action.trackXOnly && !action.trackZOnly) targetP.z = startPos.z;
                        else if (!action.trackXOnly && action.trackZOnly) targetP.x = startPos.x;
                        
                        targetP.y = 0f;
                        
                        Vector3 offsetP = GetOffsetPosition(startPos, targetP, action.targetOffset);
                        dir = offsetP - startPos;
                    }
                }
            }

            float xDir = dir.x;
            
            if (Mathf.Abs(xDir) > 0.01f)
            {
                Gizmos.color = Color.cyan; 
                Vector3 xDirection = new Vector3(Mathf.Sign(xDir), 0, 0);
                
                DrawArrow(startPos, xDirection * 2f);
            }
        }
    }

    private void DrawArrow(Vector3 pos, Vector3 direction)
    {
        if (direction == Vector3.zero) return;

        Gizmos.DrawRay(pos, direction);

        float arrowHeadLength = 0.5f;
        float arrowHeadAngle = 30.0f;

        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * Vector3.forward;
        
        Gizmos.DrawRay(pos + direction, right * arrowHeadLength);
        Gizmos.DrawRay(pos + direction, left * arrowHeadLength);
    }

    private void DrawDashedLine(Vector3 from, Vector3 to, float dashLength)
    {
        Vector3 direction = to - from;
        float totalLength = direction.magnitude;
        if (totalLength < 0.001f) return;
        Vector3 dir = direction / totalLength;
        float drawn = 0f;
        bool draw = true;
        while (drawn < totalLength)
        {
            float segLen = Mathf.Min(dashLength, totalLength - drawn);
            if (draw) Gizmos.DrawLine(from + dir * drawn, from + dir * (drawn + segLen));
            drawn += segLen;
            draw = !draw;
        }
    }

    private void DrawWireCylinder(Vector3 center, float radius, float height)
    {
        float halfHeight = height * 0.5f;
        Vector3 topCenter = center + Vector3.up * halfHeight;
        Vector3 bottomCenter = center - Vector3.up * halfHeight;

        DrawGizmoCircle(topCenter, radius);
        DrawGizmoCircle(bottomCenter, radius);

        Gizmos.DrawLine(topCenter + Vector3.right * radius, bottomCenter + Vector3.right * radius);
        Gizmos.DrawLine(topCenter - Vector3.right * radius, bottomCenter - Vector3.right * radius);
        Gizmos.DrawLine(topCenter + Vector3.forward * radius, bottomCenter + Vector3.forward * radius);
        Gizmos.DrawLine(topCenter - Vector3.forward * radius, bottomCenter - Vector3.forward * radius);
    }

    private void DrawGizmoCircle(Vector3 center, float radius)
    {
        int segments = 24;
        float angle = 0f;
        Vector3 lastPoint = center + new Vector3(Mathf.Cos(0) * radius, 0, Mathf.Sin(0) * radius);
        for (int i = 1; i <= segments; i++)
        {
            angle += (360f / segments) * Mathf.Deg2Rad;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(lastPoint, nextPoint);
            lastPoint = nextPoint;
        }
    }
}