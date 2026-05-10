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
    
    // 현재 실행 중인 액션들을 추적하기 위한 컬렉션 (기즈모 표시 등에 활용)
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

    // 액션의 시작과 끝을 추적하여 activeActions에 등록/해제하는 코루틴 래퍼
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
            case ActionType.Teleport: return new TeleportState(this, action);
            case ActionType.Wait: return new WaitState(this, action);
            case ActionType.Attack: return new AttackState(this, action);
            default: return null;
        }
    }

    // 이동 방향에 맞춰 캐릭터를 Y축 기준으로 회전시키는 함수
    private void UpdateFacingDirection(float xDir)
    {
        if (Mathf.Abs(xDir) > 0.01f)
        {
            // 오른쪽(양수)일 때는 180도, 왼쪽(음수)일 때는 0도로 회전
            float yRotation = xDir > 0 ? 180f : 0f;
            transform.rotation = Quaternion.Euler(0, yRotation, 0);
        }
    }

    // ▼ 이동할 방향을 가장 가까운 8방향(45도 간격)으로 고정(스냅)해 주는 함수
    private Vector3 Get8Direction(Vector3 dir)
    {
        Vector3 flatDir = new Vector3(dir.x, 0, dir.z);
        if (flatDir.sqrMagnitude < 0.001f) return dir.normalized; // 방향이 없거나 완전히 수직인 경우 무시

        // X, Z 축 기준으로 각도를 구해 45도 단위로 반올림
        float angle = Mathf.Atan2(flatDir.z, flatDir.x) * Mathf.Rad2Deg;
        float snappedAngle = Mathf.Round(angle / 45f) * 45f;
        float rad = snappedAngle * Mathf.Deg2Rad;

        // 8방향에 해당하는 새로운 벡터 생성
        Vector3 snappedDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
        
        // 기존 Y축(높이) 방향은 자연스럽게 유지
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
        
        // 시작할 때부터 8방향으로 꺾인 벡터를 사용
        Vector3 normalizedDir = Get8Direction(direction.normalized);

        // 이동 전 방향 업데이트
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
            rb.MovePosition(nextPos);
            
            yield return new WaitForFixedUpdate();
        }
    }

    public IEnumerator MoveToPosition(Vector3 targetPos, float startSpeed, float maxSpeed, bool isAccelerated, float accel, bool checkTarget, float stopDist, bool checkTime, float timeLimit, DistanceCheckMode distanceMode, TargetShape targetShape, Vector3 shapeOffset, Vector3 attackBoxSize)
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

            if (checkTarget && CheckTargetReached(targetPos, distanceMode, targetShape, stopDist, shapeOffset, attackBoxSize)) 
                yield break;

            Vector3 destination = targetPos;
            if (distanceMode == DistanceCheckMode.AttackBox)
            {
                Vector3 worldOffset = transform.TransformPoint(shapeOffset) - transform.position;
                destination = targetPos - worldOffset;
            }

            // ▼ 목적지까지의 방향을 계산하고 8방향으로 고정
            Vector3 dirToDest = destination - rb.position;
            float distToDest = dirToDest.magnitude;
            
            Vector3 moveDir = Get8Direction(dirToDest);

            // 이동 전 방향 업데이트
            UpdateFacingDirection(moveDir.x);

            if (isAccelerated)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, accel * Time.fixedDeltaTime);
            }

            float moveStep = currentSpeed * Time.fixedDeltaTime;
            Vector3 nextPos;

            // 목적지에 거의 도달했을 때 8방향 스냅 때문에 목적지 주변을 맴도는 현상(Jittering) 방지
            if (distToDest <= moveStep)
            {
                nextPos = destination;
            }
            else
            {
                nextPos = rb.position + (moveDir * moveStep);
            }
            
            rb.MovePosition(nextPos);
            
            yield return new WaitForFixedUpdate();
        }
    }

    public IEnumerator TrackObject(Transform target, bool trackXOnly, bool trackZOnly, float trackMargin, float startSpeed, float maxSpeed, bool isAccelerated, float accel, bool checkTarget, float stopDist, bool checkTime, float timeLimit, DistanceCheckMode distanceMode, TargetShape targetShape, Vector3 shapeOffset, Vector3 attackBoxSize)
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

            Vector3 targetPos = target.position;

            if (trackXOnly && !trackZOnly)
                targetPos.z = rb.position.z; 
            else if (!trackXOnly && trackZOnly)
                targetPos.x = rb.position.x; 

            Vector3 directionToMe = (rb.position - targetPos).normalized;
            Vector3 baseTargetPos = targetPos + (directionToMe * trackMargin);

            if (checkTarget && CheckTargetReached(baseTargetPos, distanceMode, targetShape, stopDist, shapeOffset, attackBoxSize)) 
                yield break;

            Vector3 destination = baseTargetPos;
            if (distanceMode == DistanceCheckMode.AttackBox)
            {
                Vector3 worldOffset = transform.TransformPoint(shapeOffset) - transform.position;
                destination = baseTargetPos - worldOffset;
            }

            if (trackXOnly && !trackZOnly)
                destination.z = rb.position.z;
            else if (!trackXOnly && trackZOnly)
                destination.x = rb.position.x;

            // ▼ 추적할 때의 방향도 계산하여 8방향으로 고정
            Vector3 dirToDest = destination - rb.position;
            float distToDest = dirToDest.magnitude;

            Vector3 moveDir = Get8Direction(dirToDest);

            // 이동 전 방향 업데이트
            UpdateFacingDirection(moveDir.x);

            if (isAccelerated)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, accel * Time.fixedDeltaTime);
            }

            float moveStep = currentSpeed * Time.fixedDeltaTime;
            Vector3 nextPos;

            // 목적지에 거의 도달했을 때 맴도는 현상 방지 보정
            if (distToDest <= moveStep)
            {
                nextPos = destination;
            }
            else
            {
                nextPos = rb.position + (moveDir * moveStep);
            }

            rb.MovePosition(nextPos);
            
            yield return new WaitForFixedUpdate();
        }
    }

    private bool CheckTargetReached(Vector3 targetPos, DistanceCheckMode mode, TargetShape shape, float floatDist, Vector3 shapeOffset, Vector3 attackBoxSize)
    {
        Vector3 currentPos = rb.position;

        if (mode == DistanceCheckMode.Distance3D)
        {
            Vector3 center = targetPos + shapeOffset;
            
            if (shape == TargetShape.Point)
            {
                return Vector3.Distance(currentPos, center) <= floatDist;
            }
            else if (shape == TargetShape.Sphere)
            {
                Vector3 closestPoint = col.ClosestPoint(center);
                return Vector3.Distance(closestPoint, center) <= floatDist;
            }
            else 
            {
                float diffX = Mathf.Abs(center.x - currentPos.x);
                float diffY = Mathf.Abs(center.y - currentPos.y);
                float diffZ = Mathf.Abs(center.z - currentPos.z);
                return diffX <= attackBoxSize.x * 0.5f && diffY <= attackBoxSize.y * 0.5f && diffZ <= attackBoxSize.z * 0.5f;
            }
        }
        else if (mode == DistanceCheckMode.AttackBox)
        {
            Vector3 localTargetPos = transform.InverseTransformPoint(targetPos) - shapeOffset;
            
            if (shape == TargetShape.Point)
            {
                return localTargetPos.magnitude <= floatDist;
            }
            else if (shape == TargetShape.Sphere)
            {
                return localTargetPos.magnitude <= floatDist;
            }
            else 
            {
                bool inX = (Mathf.Abs(localTargetPos.x) <= attackBoxSize.x * 0.5f);
                bool inY = (Mathf.Abs(localTargetPos.y) <= attackBoxSize.y * 0.5f);
                bool inZ = (Mathf.Abs(localTargetPos.z) <= attackBoxSize.z * 0.5f);
                return inX && inY && inZ;
            }
        }
        
        return false;
    }

    public void TeleportToPosition(Vector3 targetPos)
    {
        // 텔레포트 전 방향 업데이트
        UpdateFacingDirection((targetPos - rb.position).x);

        rb.linearVelocity = Vector3.zero; 
        rb.position = targetPos; 
    }

    public void TeleportToObject(Transform target, bool trackXOnly, bool trackZOnly, Vector3 offset)
    {
        if (target != null)
        {
            Vector3 targetPos = target.position;

            if (trackXOnly && !trackZOnly)
                targetPos.z = rb.position.z; 
            else if (!trackXOnly && trackZOnly)
                targetPos.x = rb.position.x; 

            Vector3 destination = targetPos + offset;

            // 텔레포트 전 방향 업데이트
            UpdateFacingDirection((destination - rb.position).x);

            rb.linearVelocity = Vector3.zero;
            rb.position = destination;
        }
    }

    private void OnDrawGizmos()
    {
        if (actionSequence == null || actionSequence.actions == null) return;

        foreach (var action in actionSequence.actions)
        {
            if (!action.showGizmo) continue;

            if (action.actionType == ActionType.Attack)
            {
                // 플레이 모드 중일 때는 해당 공격 액션이 활성화 상태일 때만 기즈모를 그림
                // (에디터 모드일 때는 범위 세팅을 위해 항상 표시됨)
                if (Application.isPlaying && !activeActions.Contains(action)) continue;

                Gizmos.color = new Color(1f, 0f, 0f, 0.4f); 
                
                // 회전 기반 방식이므로 localScale은 제외하고 TRS 매트릭스 구성
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

                Gizmos.matrix = Matrix4x4.identity;
                continue; 
            }

            if (action.targetType == TargetType.Direction)
            {
                Gizmos.color = Color.magenta;
                Vector3 dir = action.moveDirection.normalized;
                Gizmos.DrawRay(transform.position, dir * 3f);
                Gizmos.DrawSphere(transform.position + dir * 3f, 0.15f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(transform.position + dir * 3f + Vector3.up * 0.5f, $"방향 이동\n({action.moveDirection})");
#endif
                continue;
            }

            Vector3 finalTargetPos = action.targetPosition;

            if (action.targetType == TargetType.TrackObject)
            {
                if (!string.IsNullOrEmpty(action.targetTag))
                {
                    GameObject targetGO = GameObject.FindWithTag(action.targetTag);
                    if (targetGO != null) 
                    {
                        Vector3 targetPos = targetGO.transform.position;

                        if (action.trackXOnly && !action.trackZOnly)
                            targetPos.z = transform.position.z;
                        else if (!action.trackXOnly && action.trackZOnly)
                            targetPos.x = transform.position.x;

                        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                        
                        float gizmoSize = action.trackMargin > 0 ? action.trackMargin : 0.15f;

                        if (action.targetGizmoShape == TargetGizmoShape.Sphere)
                        {
                            if (action.trackMargin <= 0) Gizmos.DrawSphere(targetPos, gizmoSize);
                            else Gizmos.DrawWireSphere(targetPos, gizmoSize);
                        }
                        else if (action.targetGizmoShape == TargetGizmoShape.Box)
                        {
                            if (action.trackMargin <= 0) Gizmos.DrawCube(targetPos, Vector3.one * gizmoSize * 2f);
                            else Gizmos.DrawWireCube(targetPos, Vector3.one * gizmoSize * 2f);
                        }

                        Vector3 directionToMe = (transform.position - targetPos).normalized;
                        float marginDistance = action.trackMargin;
                        finalTargetPos = targetPos + (directionToMe * marginDistance);
                    }
                }
            }
            else
            {
                finalTargetPos += action.offset;
            }

            Vector3 destination = finalTargetPos;
            if (action.distanceMode == DistanceCheckMode.AttackBox)
            {
                Vector3 worldOffset = transform.TransformPoint(action.shapeOffset) - transform.position;
                destination = finalTargetPos - worldOffset;
            }

            if (action.targetType == TargetType.TrackObject)
            {
                if (action.trackXOnly && !action.trackZOnly)
                    destination.z = transform.position.z;
                else if (!action.trackXOnly && action.trackZOnly)
                    destination.x = transform.position.x;
            }

            Gizmos.color = action.actionType == ActionType.Teleport ? Color.cyan : Color.yellow;
            Gizmos.DrawSphere(destination, 0.1f);

            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.3f);
            Gizmos.DrawLine(transform.position, destination);

            if (action.actionType == ActionType.Move && action.stopOnTargetReached)
            {
                Gizmos.color = Color.green;

                if (action.distanceMode == DistanceCheckMode.Distance3D)
                {
                    Vector3 center = finalTargetPos + action.shapeOffset;
                    
                    if (action.targetShape == TargetShape.Point)
                    {
                        Gizmos.DrawWireSphere(center, action.stopDistance);
                    }
                    else if (action.targetShape == TargetShape.Sphere)
                    {
                        Gizmos.DrawWireSphere(center, action.stopDistance);
                    }
                    else
                    {
                        Gizmos.DrawWireCube(center, action.attackBoxSize);
                        Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
                        Gizmos.DrawCube(center, action.attackBoxSize);
                    }
                }
                else if (action.distanceMode == DistanceCheckMode.AttackBox)
                {
                    Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.localScale);
                    Gizmos.matrix = rotationMatrix;

                    Vector3 center = action.shapeOffset;

                    if (action.targetShape == TargetShape.Point)
                    {
                        Gizmos.DrawWireSphere(center, action.stopDistance);
                    }
                    else if (action.targetShape == TargetShape.Sphere)
                    {
                        Gizmos.DrawWireSphere(center, action.stopDistance);
                    }
                    else
                    {
                        Gizmos.DrawWireCube(center, action.attackBoxSize);
                        Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
                        Gizmos.DrawCube(center, action.attackBoxSize);
                    }
                }
                else if (action.distanceMode == DistanceCheckMode.AttackBox)
                {
                    Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.localScale);
                    Gizmos.matrix = rotationMatrix;

                    Vector3 center = action.shapeOffset;

                    if (action.targetShape == TargetShape.Sphere)
                    {
                        Gizmos.DrawWireSphere(center, action.stopDistance);
                    }
                    else
                    {
                        Gizmos.DrawWireCube(center, action.attackBoxSize);
                        Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
                        Gizmos.DrawCube(center, action.attackBoxSize);
                    }
                    
                    Gizmos.matrix = Matrix4x4.identity;
                }
            }

#if UNITY_EDITOR
            UnityEditor.Handles.Label(destination + Vector3.up * 0.5f, $"목표: {action.actionType}\n(모드: {action.distanceMode})");
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
                dir = action.moveDirection;
            }
            else if (action.targetType == TargetType.SpecificPosition)
            {
                dir = (action.targetPosition + action.offset) - startPos;
            }
            else 
            {
                if (!string.IsNullOrEmpty(action.targetTag))
                {
                    GameObject targetGO = GameObject.FindWithTag(action.targetTag);
                    if (targetGO != null)
                    {
                        dir = targetGO.transform.position - startPos;
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
}