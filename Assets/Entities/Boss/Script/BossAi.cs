﻿using System.Collections;
using UnityEngine;

public class BossAI : MonoBehaviour
{
    private Boss Boss;
    
    [Header("추격 타겟")]
    public Transform target; 

    [Header("거리 설정")]
    public float detectionRadius = 10f; 
    
    [Tooltip("코루틴 내에서 직접 이동시킬 때 사용할 속도")]
    public float moveSpeed = 3f;

    // 현재 실행할 특수 패턴 번호 (1, 2, 3 순환)
    private int currentSpecialPattern = 1;
    private bool isPatternRunning = false;

    private void Awake()
    {
        Boss = GetComponent<Boss>();
    }

    private void Start()
    {
        if (!target)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) target = playerObj.transform;
        }
    }

    private void Update()
    {
        if (!target) return;

        // 패턴이 아직 실행되지 않았고, 플레이어가 감지 범위 내에 들어오면 패턴 사이클 시작
        if (!isPatternRunning && IsPlayerDetected())
        {
            StartCoroutine(PatternCycleRoutine());
        }
    }

    private bool IsPlayerDetected()
    {
        // 2.5D 환경에 맞게 Y축을 0으로 맞추어 X, Z 평면상의 거리만 측정
        Vector3 myPos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 targetPos = new Vector3(target.position.x, 0, target.position.z);
        return Vector3.Distance(myPos, targetPos) <= detectionRadius;
    }

    /// <summary>
    /// 전체 행동 사이클을 관리하는 코루틴
    /// </summary>
    private IEnumerator PatternCycleRoutine()
    {
        isPatternRunning = true;

        while (true)
        {
            // 1. 기본 패턴 실행
            yield return StartCoroutine(BasicPatternRoutine());

            // 2. 특수 패턴 순차적 실행
            switch (currentSpecialPattern)
            {
                case 1:
                    yield return StartCoroutine(Pattern1Routine());
                    currentSpecialPattern = 2; 
                    break;
                case 2:
                    yield return StartCoroutine(Pattern1Routine());
                    currentSpecialPattern = 3; 
                    break;
                case 3:
                    yield return StartCoroutine(Pattern1Routine());
                    currentSpecialPattern = 1; 
                    break;
            }
        }
    }

    /// <summary>
    /// 기본 행동 패턴
    /// </summary>
    private IEnumerator BasicPatternRoutine()
    {
        // 1. 2초간 가만히 서서 플레이어 바라보기
        yield return StartCoroutine(WaitAndFacePlayer(2f));
        
        // 2. 플레이어 방향으로 2.5초간 걷기
        Vector3 dirToPlayer = Get8WayDirectionToTarget();
        yield return StartCoroutine(MoveForDuration(dirToPlayer, 2.5f));
        
        // 3. 플레이어 반대 방향으로 2초간 걷기
        Vector3 dirFromPlayer = -Get8WayDirectionToTarget();
        yield return StartCoroutine(MoveForDuration(dirFromPlayer, 2f));
        
        // 4. 상단 방향(Z+)으로 2초 움직이기 
        yield return StartCoroutine(MoveForDuration(Vector3.forward, 2f));
        
        // 5. 하단 방향(Z-)으로 2초 이동
        yield return StartCoroutine(MoveForDuration(Vector3.back, 2f));

        // 6. 해당 행동들 종료 후 1초간 서서 플레이어 바라보기
        yield return StartCoroutine(WaitAndFacePlayer(1f));
    }

    /// <summary>
    /// 가만히 서 있는 동안 매 프레임 플레이어를 향해 회전하는 코루틴
    /// </summary>
    private IEnumerator WaitAndFacePlayer(float duration)
    {
        Boss.StateMachine.ChangeState(Boss.StateMachine.IdleState);
        
        float timer = 0f;
        while (timer < duration)
        {
            if (target)
            {
                // 실시간으로 플레이어 방향을 계산하여 바라봄
                Vector3 dir = Get8WayDirectionToTarget();
                if (dir != Vector3.zero)
                {
                    Boss.Movement.FaceTarget(dir);
                }
            }
            
            timer += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 지정된 방향으로 2.5D(X, Z축) 기반 이동을 처리하는 코루틴
    /// </summary>
    /// <summary>
    /// 지정된 방향으로 2.5D(X, Z축) 기반 이동을 처리하는 코루틴
    /// speedMultiplier를 통해 기본 이동 속도보다 빠르거나 느리게 움직일 수 있습니다.
    /// </summary>
    private IEnumerator MoveForDuration(Vector3 fixedDirection, float duration, float speedMultiplier = 1f)
    {
        Boss.StateMachine.ChangeState(Boss.StateMachine.MoveState); 
        
        fixedDirection.y = 0f;
        fixedDirection.Normalize();

        if (fixedDirection != Vector3.zero)
        {
            Boss.Movement.FaceTarget(fixedDirection);
        }

        float timer = 0f;
        // 기본 moveSpeed에 배수를 곱해서 최종 속도 결정
        float currentSpeed = moveSpeed * speedMultiplier;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            Vector3 nextPos = transform.position;
            nextPos.x += fixedDirection.x * currentSpeed * Time.deltaTime;
            nextPos.z += fixedDirection.z * currentSpeed * Time.deltaTime;
            nextPos.y = transform.position.y; 

            transform.position = nextPos;

            yield return null;
        }
    }

    #region 직접 구현할 특수 패턴들

    private IEnumerator Pattern1Routine()
    {
        Debug.Log("패턴 1 시작: 연속 돌진 공격");
        Boss.pattern = 1;
        Boss.StateMachine.ChangeState(Boss.StateMachine.AttackState); 

        // 1. 30프레임(1초)간 가만히 서있기
        Debug.Log("30프레임(1초)간 가만히 서있기");
        yield return StartCoroutine(WaitForFrames(30)); 

        // 2. 플레이어 방향으로 15프레임(0.5초)간 빠르게 직선 이동
        Debug.Log("플레이어 방향으로 15프레임(0.5초)간 빠르게 직선 이동");
        Vector3 directToPlayer1 = GetDirectDirectionToTarget();
        yield return StartCoroutine(MoveForFrames(directToPlayer1, 15, 10f));

        Debug.Log("20프레임 대기");
        yield return StartCoroutine(WaitForFrames(20)); 
        
        // 6. 플레이어 쪽으로 15프레임(0.5초)간 빠르게 직선 이동
        Debug.Log("플레이어 쪽으로 15프레임(0.5초)간 빠르게 직선 이동");
        Vector3 directToPlayer2 = GetDirectDirectionToTarget();
        yield return StartCoroutine(MoveForFrames(directToPlayer2, 15, 10f));
        
        Debug.Log("25프레임 대기");
        yield return StartCoroutine(WaitForFrames(25)); 
    }

    private IEnumerator Pattern2Routine()
    {
        Debug.Log("패턴 2 실행");
        yield return new WaitForSeconds(1f);
    }

    private IEnumerator Pattern3Routine()
    {
        Debug.Log("패턴 3 실행");
        yield return new WaitForSeconds(1f);
    }

    #endregion

    /// <summary>
    /// 플레이어를 향하는 8방향 스냅 벡터를 구합니다. (Y축은 0으로 강제)
    /// </summary>
    public Vector3 Get8WayDirectionToTarget()
    {
        if (!target) return Vector3.zero;

        Vector3 direction = target.position - transform.position;
        direction.y = 0; // XZ 평면 이동 기준
        direction.Normalize();

        float snapX = Mathf.Round(direction.x);
        float snapZ = Mathf.Round(direction.z);

        return new Vector3(snapX, 0f, snapZ).normalized;
    }
    
    /// <summary>
    /// 플레이어를 향하는 정확한 직선 벡터를 구합니다. (Y축은 0으로 강제하여 2.5D 유지)
    /// </summary>
    public Vector3 GetDirectDirectionToTarget()
    {
        if (!target) return Vector3.zero;

        Vector3 direction = target.position - transform.position;
        direction.y = 0; // XZ 평면 유지
        return direction.normalized;
    }
    
    // 애니메이션 샘플링 레이트 (1초 = 30프레임)
    private const float AnimFPS = 30f;

    /// <summary>
    /// 애니메이션 프레임을 실제 시간(초)으로 변환합니다.
    /// </summary>
    private float FrameToSec(int frames)
    {
        return frames / AnimFPS;
    }

    /// <summary>
    /// 지정된 애니메이션 프레임만큼 애니메이터 시간에 맞춰 대기합니다.
    /// </summary>
    private IEnumerator WaitForFrames(int frames)
    {
        yield return new WaitForSeconds(FrameToSec(frames));
    }
    
    /// <summary>
    /// 애니메이션 프레임을 기준으로 2.5D 이동을 처리하는 코루틴
    /// </summary>
    // private IEnumerator MoveForFrames(Vector3 fixedDirection, int frames, float speedMultiplier = 1f)
    // {
    //     fixedDirection.y = 0f;
    //     fixedDirection.Normalize();
    //
    //     if (fixedDirection != Vector3.zero)
    //     {
    //         Boss.Movement.FaceTarget(fixedDirection);
    //     }
    //
    //     float timer = 0f;
    //     // 입력받은 프레임을 실제 초 단위 시간으로 변환
    //     float targetDuration = FrameToSec(frames); 
    //     float currentSpeed = moveSpeed * speedMultiplier;
    //
    //     while (timer < targetDuration)
    //     {
    //         timer += Time.deltaTime;
    //
    //         Vector3 nextPos = transform.position;
    //         nextPos.x += fixedDirection.x * currentSpeed * Time.deltaTime;
    //         nextPos.z += fixedDirection.z * currentSpeed * Time.deltaTime;
    //         nextPos.y = transform.position.y; 
    //
    //         transform.position = nextPos;
    //
    //         yield return null;
    //     }
    // }
    
    private IEnumerator MoveForFrames(Vector3 fixedDirection, int frames, float speedMultiplier = 1f)
    {
        fixedDirection.y = 0f;
        fixedDirection.Normalize();

        if (fixedDirection != Vector3.zero)
        {
            Boss.Movement.FaceTarget(fixedDirection);
        }

        float timer = 0f;
        float targetDuration = FrameToSec(frames); 
        float currentSpeed = moveSpeed * speedMultiplier;
    
        // 플레이어와 "닿았다"고 판단할 임계치 (보스 크기에 따라 조절하세요)
        float stopDistance = 1.2f; 

        while (timer < targetDuration)
        {
            // --- 추가된 충돌 체크 로직 ---
            if (target != null)
            {
                Vector3 myPos = new Vector3(transform.position.x, 0, transform.position.z);
                Vector3 targetPos = new Vector3(target.position.x, 0, target.position.z);
            
                if (Vector3.Distance(myPos, targetPos) <= stopDistance)
                {
                    Debug.Log("이동 중 플레이어 감지: 이동 중단 및 대기");
                
                    // 남은 시간만큼 아무것도 안 하고 대기
                    float remainingTime = targetDuration - timer;
                    if (remainingTime > 0)
                    {
                        yield return new WaitForSeconds(remainingTime);
                    }
                    yield break; // 코루틴 완전히 종료
                }
            }
            // --------------------------

            timer += Time.deltaTime;

            Vector3 nextPos = transform.position;
            nextPos.x += fixedDirection.x * currentSpeed * Time.deltaTime;
            nextPos.z += fixedDirection.z * currentSpeed * Time.deltaTime;
            nextPos.y = transform.position.y; 

            transform.position = nextPos;

            yield return null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(new Vector3(transform.position.x, 0, transform.position.z), detectionRadius);
    }
}
