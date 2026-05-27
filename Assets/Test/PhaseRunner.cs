using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 패턴 선택 시점의 스냅샷 정보 (디버그 모니터용)
/// </summary>
public struct PhaseSelectionInfo
{
    public float time;
    public float distance;
    public Vector3 relativePos;
    public int selectedIndex;
    public string selectedName;
    public int lastIndex;
    public float[] weights;
    public float[] probabilities;
    public float totalWeight;
    public string[] entryNames;
}

/// <summary>
/// PhaseSO를 런타임에서 실행하는 컴포넌트.
/// 가중치 기반 시퀀스 선택 + 콤보 연쇄를 처리한다.
/// </summary>
public class PhaseRunner : MonoBehaviour
{
    [Header("페이즈 설정")]
    [SerializeField] private PhaseSO phase;
    [SerializeField] private bool playOnStart = true;

    [Header("참조")]
    [SerializeField] private CapsuleController controller;

    // ── 런타임 상태 ──
    private int lastSelectedIndex = -1;
    public int LastSelectedIndex => lastSelectedIndex;
    private bool isRunning;

    // ── 중단 토큰 ──
    private InterruptToken currentToken;

    // ── 타겟 캐싱 ──
    private Transform cachedTarget;

    /// <summary>
    /// 패턴이 선택될 때마다 발행되는 이벤트 (디버그 모니터 연동용)
    /// </summary>
    public event Action<PhaseSelectionInfo> OnSequenceSelected;
    
    // ── 콤보 판정용 ──
    private bool hitConfirmed;

    // AttackCaster 참조 (캐싱)
    private AttackCaster attackCaster;


    private void Awake()
    {
        if (!controller)
            controller = GetComponent<CapsuleController>();

        attackCaster = GetComponent<AttackCaster>();
    }

    private void Start()
    {
        CacheTarget();
        // ── PhaseManager가 부착되어 있다면 매니저가 주도권을 갖고 첫 페이즈를 기동하므로 이중 시작을 억제합니다. ──
        if (playOnStart && phase && !GetComponent<PhaseManager>())
            StartPhase();
    }

    // ── 타겟 캐싱 ──
    private void CacheTarget()
    {
        if (!phase || string.IsNullOrEmpty(phase.targetTag)) return;
        GameObject targetObj = GameObject.FindWithTag(phase.targetTag);
        if (targetObj) cachedTarget = targetObj.transform;
    }

    private void OnEnable()
    {
        if (attackCaster)
            attackCaster.OnHitConfirmed += HandleHitConfirmed;
    }

    private void OnDisable()
    {
        if (attackCaster)
            attackCaster.OnHitConfirmed -= HandleHitConfirmed;
    }

    // ── 외부 API ──
    public void StartPhase()
    {
        if (isRunning) return;
        StartCoroutine(PhaseLoop());
    }

    public void StopPhase()
    {
        isRunning = false;
        currentToken?.Interrupt(InterruptReason.External);
        StopAllCoroutines();
    }

    /// <summary>
    /// 현재 실행 중인 시퀀스를 안전하게 중단합니다.
    /// 그로기 진입 / 페이즈 전환 시 외부에서 호출하세요.
    /// </summary>
    public void InterruptCurrentSequence(InterruptReason reason = InterruptReason.External)
    {
        currentToken?.Interrupt(reason);
    }

    /// <summary>
    /// 런타임에서 페이즈 SO를 교체합니다 (PhaseManager 연동용).
    /// </summary>
    public void SetPhase(PhaseSO newPhase)
    {
        phase = newPhase;
        lastSelectedIndex = -1;
        CacheTarget();
    }

    // ── 타격 성공 콜백 ──
    private void HandleHitConfirmed()
    {
        hitConfirmed = true;
    }

    // ═══════════════════════════════════════════════════════════
    // 메인 페이즈 루프
    // ═══════════════════════════════════════════════════════════
    private IEnumerator PhaseLoop()
    {
        isRunning = true;
        int repeatDone = 0;

        while (isRunning && (phase.isInfiniteLoop || repeatDone < phase.repeatCount))
        {
            // 1) 가중치 기반으로 엔트리 선택
            int selectedIndex = SelectEntryByWeight();
            if (selectedIndex < 0)
            {
                Debug.LogWarning("[PhaseRunner] 선택 가능한 엔트리가 없습니다.");
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            PhaseEntry entry = phase.entries[selectedIndex];
            lastSelectedIndex = selectedIndex;

            // 2) 선택된 시퀀스 실행
            yield return StartCoroutine(ExecuteActionSequence(entry.actionSequence));

            // 3) 콤보 처리
            if (entry.isComboStarter && entry.comboFollowUps != null && entry.comboFollowUps.Count > 0)
            {
                // 콤보 시작 시퀀스 실행 후 타격 성공 여부 확인
                if (hitConfirmed)
                {
                    // 후속 콤보 시퀀스들을 순차 실행
                    for (int c = 0; c < entry.comboFollowUps.Count; c++)
                    {
                        ActionSequenceSO followUp = entry.comboFollowUps[c];
                        if (followUp == null) continue;

                        hitConfirmed = false; // 다음 콤보를 위해 리셋

                        yield return StartCoroutine(ExecuteActionSequence(followUp));

                        // 마지막 후속 콤보가 아니라면, 이번 타격도 성공해야 다음 콤보 진행
                        if (c < entry.comboFollowUps.Count - 1 && !hitConfirmed)
                        {
                            // 타격 실패 → 콤보 끊김
                            break;
                        }
                    }
                }

                // 콤보 종료 후 리셋
                hitConfirmed = false;
            }
            else
            {
                // 비콤보 엔트리는 히트 플래그 리셋
                hitConfirmed = false;
            }

            repeatDone++;
        }

        isRunning = false;
    }

    // ═══════════════════════════════════════════════════════════
    // 가중치 기반 선택
    // ═══════════════════════════════════════════════════════════
    private int SelectEntryByWeight()
    {
        if (phase.entries == null || phase.entries.Count == 0) return -1;

        Vector3 relPos = GetRelativePositionToPlayer();
        float distanceToPlayer = relPos.magnitude;
        float totalWeight = 0f;

        // 각 엔트리별 최종 가중치 계산
        float[] weights = new float[phase.entries.Count];
        string[] names = new string[phase.entries.Count];

        for (int i = 0; i < phase.entries.Count; i++)
        {
            PhaseEntry entry = phase.entries[i];
            names[i] = entry.actionSequence ? entry.actionSequence.name : "(없음)";
            if (!entry.actionSequence) continue;

            float w = entry.baseWeight;

            // 거리 기반 곱수
            w *= entry.EvaluateDistanceMultiplier(relPos, transform.forward);

            // 반복 패널티
            if (i == lastSelectedIndex)
                w *= phase.repeatPenalty;

            // 음수 방지
            w = Mathf.Max(0f, w);

            weights[i] = w;
            totalWeight += w;
        }

        if (totalWeight <= 0f) return -1;

        // 확률 배열 계산
        float[] probs = new float[weights.Length];
        for (int i = 0; i < weights.Length; i++)
            probs[i] = weights[i] / totalWeight;

        // 룰렛 휠 선택
        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;
        int selected = weights.Length - 1;

        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                selected = i;
                break;
            }
        }

        // 선택 이벤트 발행
        OnSequenceSelected?.Invoke(new PhaseSelectionInfo
        {
            time = Time.time,
            distance = distanceToPlayer,
            relativePos = relPos,
            selectedIndex = selected,
            selectedName = names[selected],
            lastIndex = lastSelectedIndex,
            weights = weights,
            probabilities = probs,
            totalWeight = totalWeight,
            entryNames = names
        });

        return selected;
    }

    // ═══════════════════════════════════════════════════════════
    // 액션 시퀀스 실행 (CapsuleController의 기존 로직 재활용)
    // ═══════════════════════════════════════════════════════════
    private IEnumerator ExecuteActionSequence(ActionSequenceSO sequence)
    {
        if (!sequence || !controller) yield break;

        // 매 시퀀스마다 새 토큰 생성
        currentToken = new InterruptToken();

        var parallelCoroutines = new System.Collections.Generic.List<Coroutine>();

        foreach (var action in sequence.actions)
        {
            if (currentToken.IsInterrupted) yield break;

            ActionState state = CreateState(action, currentToken);
            if (state == null) continue;

            // 공격 액션이면 hitConfirmed 리셋
            bool isAttackAction = action.actionType == ActionType.VariableAttack
                || action.actionType == ActionType.FixedAttack
                || action.actionType == ActionType.RangedAttack;

            if (isAttackAction)
                hitConfirmed = false;

            if (action.executeParallel)
            {
                Coroutine c = controller.StartCoroutine(state.Execute());
                parallelCoroutines.Add(c);
            }
            else
            {
                yield return controller.StartCoroutine(state.Execute());
            }
        }

        // 병렬 실행 코루틴 완료 대기
        foreach (var c in parallelCoroutines)
        {
            if (c != null) yield return c;
        }
    }

    private ActionState CreateState(ActionData action, InterruptToken token)
    {
        switch (action.actionType)
        {
            case ActionType.Move:          return new MoveState(controller, action, token, cachedTarget);
            case ActionType.Wait:          return new WaitState(controller, action, token, cachedTarget);
            case ActionType.VariableAttack:
            case ActionType.FixedAttack:   return new AttackState(controller, action, token, cachedTarget);
            case ActionType.RangedAttack:  return new RangedAttackState(controller, action, token, cachedTarget);
            default: return null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 유틸
    // ═══════════════════════════════════════════════════════════
    private Vector3 GetRelativePositionToPlayer()
    {
        if (!phase || string.IsNullOrEmpty(phase.targetTag)) return Vector3.zero;

        // 캐싱된 타겟 우선 사용 → FindWithTag 주기 최소화
        if (!cachedTarget)
        {
            CacheTarget();
            if (!cachedTarget) return Vector3.zero;
        }

        Vector3 diff = cachedTarget.position - transform.position;
        diff.y = 0f;
        return diff;
    }

    // ═══════════════════════════════════════════════════════════
    // 기즈모 (씬 뷰 + 플레이 모드 모두 표시)
    // ═══════════════════════════════════════════════════════════
    private static readonly Color[] GizmoColors =
    {
        new Color(1f,  0.3f, 0.3f, 1f),  // 빨강
        new Color(0.3f, 0.7f, 1f,  1f),  // 파랑
        new Color(0.3f, 1f,  0.3f, 1f),  // 초록
        new Color(1f,  0.85f, 0.1f, 1f), // 노랑
        new Color(0.8f, 0.3f, 1f,  1f),  // 보라
        new Color(1f,  0.55f, 0.1f, 1f), // 주황
    };

    private void OnDrawGizmos()
    {
        if (!phase) return;

        for (int i = 0; i < phase.entries.Count; i++)
        {
            PhaseEntry entry = phase.entries[i];
            Color col = GizmoColors[i % GizmoColors.Length];

            // 메인 시퀀스 공격 기즈모
            if (entry.showGizmos && entry.actionSequence)
            {
                foreach (var action in entry.actionSequence.actions)
                    DrawActionGizmo(action, col);
            }

            // 거리 범위 기즈모
            if (entry.showRangeGizmo)
                DrawRangeGizmo(entry, col);

            // 콤보 후속 시퀀스 기즈모 (각각 독립 토글)
            if (entry.comboFollowUps != null)
            {
                // 후속은 같은 색이지만 조금 더 투명하게 구분
                Color comboCol = new Color(col.r, col.g, col.b, col.a * 0.6f);
                for (int j = 0; j < entry.comboFollowUps.Count; j++)
                {
                    bool showComboGizmo = entry.comboFollowUpGizmos != null
                        && j < entry.comboFollowUpGizmos.Count
                        && entry.comboFollowUpGizmos[j];

                    if (showComboGizmo && entry.comboFollowUps[j])
                    {
                        foreach (var action in entry.comboFollowUps[j].actions)
                            DrawActionGizmo(action, comboCol);
                    }
                }
            }
        }
    }

    private void DrawActionGizmo(ActionData action, Color color)
    {
        if (action.actionType != ActionType.VariableAttack &&
            action.actionType != ActionType.FixedAttack &&
            action.actionType != ActionType.RangedAttack)
            return;

        // 원거리: 발사 지점만 작은 구로 표시
        if (action.actionType == ActionType.RangedAttack)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.8f);
            Vector3 spawnPos = transform.position + (transform.rotation * action.attackOffset);
            Gizmos.DrawWireSphere(spawnPos, 0.12f);
            return;
        }

        Vector3 center = action.actionType == ActionType.FixedAttack
            ? action.targetPosition
            : transform.position + (transform.rotation * action.attackOffset);

        Gizmos.color = new Color(color.r, color.g, color.b, 0.85f);
        Color fillColor = new Color(color.r, color.g, color.b, 0.12f);

        switch (action.attackShape)
        {
            case AttackShape.Sphere:
                Gizmos.DrawWireSphere(center, action.attackRadius);
                Gizmos.color = fillColor;
                Gizmos.DrawSphere(center, action.attackRadius);
                break;

            case AttackShape.Box:
                Quaternion boxRot = action.actionType == ActionType.FixedAttack
                    ? Quaternion.identity : transform.rotation;
                Matrix4x4 prev = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(center, boxRot, Vector3.one);
                Gizmos.color = new Color(color.r, color.g, color.b, 0.85f);
                Gizmos.DrawWireCube(Vector3.zero, action.attackHitBoxSize);
                Gizmos.color = fillColor;
                Gizmos.DrawCube(Vector3.zero, action.attackHitBoxSize);
                Gizmos.matrix = prev;
                break;

            case AttackShape.Cylinder:
                Gizmos.color = new Color(color.r, color.g, color.b, 0.85f);
                DrawGizmoCylinder(center, action.attackRadius, action.attackHeight);
                break;
        }
    }

    private void DrawGizmoCylinder(Vector3 center, float radius, float height)
    {
        float halfH = height * 0.5f;
        Vector3 top = center + Vector3.up * halfH;
        Vector3 bot = center - Vector3.up * halfH;
        DrawGizmoCircle(top, radius);
        DrawGizmoCircle(bot, radius);
        Gizmos.DrawLine(top + Vector3.right   * radius, bot + Vector3.right   * radius);
        Gizmos.DrawLine(top - Vector3.right   * radius, bot - Vector3.right   * radius);
        Gizmos.DrawLine(top + Vector3.forward * radius, bot + Vector3.forward * radius);
        Gizmos.DrawLine(top - Vector3.forward * radius, bot - Vector3.forward * radius);
    }

    private void DrawGizmoCircle(Vector3 center, float radius)
    {
        int segments = 24;
        float step = 360f / segments * Mathf.Deg2Rad;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * step;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    // ─── 거리 범위 기즈모 ───────────────────────────────────────

    private void DrawRangeGizmo(PhaseEntry entry, Color color)
    {
        if (entry.distanceMode == DistanceWeightMode.Constant) return;

        float minD = entry.minDistance;
        float maxD = Mathf.Max(entry.maxDistance, minD + 0.01f);
        Vector3 origin = transform.position;

        switch (entry.distanceMode)
        {
            case DistanceWeightMode.CloseRange:
            case DistanceWeightMode.FarRange:
                // 최소 거리: 반투명 링, 최대 거리: 진한 링
                Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);
                DrawGizmoCircle(origin, minD);
                Gizmos.color = new Color(color.r, color.g, color.b, 0.85f);
                DrawGizmoCircle(origin, maxD);
                break;

            case DistanceWeightMode.XAxis_Close:
            case DistanceWeightMode.XAxis_Far:
                DrawXAxisRangeCorridor(origin, minD, maxD, entry.alignThreshold, color);
                break;

            case DistanceWeightMode.ZAxis_Close:
            case DistanceWeightMode.ZAxis_Far:
                DrawZAxisRangeCorridor(origin, minD, maxD, entry.alignThreshold, color);
                break;
        }
    }

    /// <summary>X축 돌진 모드: Z 정렬 통로 + X 거리 범위를 고통로 표시</summary>
    private void DrawXAxisRangeCorridor(Vector3 origin, float minD, float maxD, float alignT, Color color)
    {
        // Z 정렬 경계선 (상/하 수평선)
        Gizmos.color = new Color(color.r, color.g, color.b, 0.75f);
        Gizmos.DrawLine(origin + new Vector3(-maxD, 0f, -alignT), origin + new Vector3(+maxD, 0f, -alignT));
        Gizmos.DrawLine(origin + new Vector3(-maxD, 0f, +alignT), origin + new Vector3(+maxD, 0f, +alignT));
        // X 최대 거리 경계선 (좌/우 수직선)
        Gizmos.DrawLine(origin + new Vector3(-maxD, 0f, -alignT), origin + new Vector3(-maxD, 0f, +alignT));
        Gizmos.DrawLine(origin + new Vector3(+maxD, 0f, -alignT), origin + new Vector3(+maxD, 0f, +alignT));
        // X 최소 거리 경계선 (안직선, 더 희리)
        Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);
        Gizmos.DrawLine(origin + new Vector3(-minD, 0f, -alignT), origin + new Vector3(-minD, 0f, +alignT));
        Gizmos.DrawLine(origin + new Vector3(+minD, 0f, -alignT), origin + new Vector3(+minD, 0f, +alignT));
    }

    /// <summary>Z축 기습 모드: X 정렬 통로 + Z 거리 범위를 고통로 표시</summary>
    private void DrawZAxisRangeCorridor(Vector3 origin, float minD, float maxD, float alignT, Color color)
    {
        // X 정렬 경계선 (좌/우 수직선)
        Gizmos.color = new Color(color.r, color.g, color.b, 0.75f);
        Gizmos.DrawLine(origin + new Vector3(-alignT, 0f, -maxD), origin + new Vector3(-alignT, 0f, +maxD));
        Gizmos.DrawLine(origin + new Vector3(+alignT, 0f, -maxD), origin + new Vector3(+alignT, 0f, +maxD));
        // Z 최대 거리 경계선 (상/하 수평선)
        Gizmos.DrawLine(origin + new Vector3(-alignT, 0f, -maxD), origin + new Vector3(+alignT, 0f, -maxD));
        Gizmos.DrawLine(origin + new Vector3(-alignT, 0f, +maxD), origin + new Vector3(+alignT, 0f, +maxD));
        // Z 최소 거리 경계선 (안직선, 더 희리)
        Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);
        Gizmos.DrawLine(origin + new Vector3(-alignT, 0f, -minD), origin + new Vector3(+alignT, 0f, -minD));
        Gizmos.DrawLine(origin + new Vector3(-alignT, 0f, +minD), origin + new Vector3(+alignT, 0f, +minD));
    }
}
