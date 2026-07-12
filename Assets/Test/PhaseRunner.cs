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

public enum AIPhaseState
{
    None,
    Idle,
    Combat,
    Return,
    Conditional
}

[Serializable]
public class ConditionalPhase
{
    public string conditionName = "특수 페이즈";
    public PhaseSO phase;
    
    [Header("체력 조건")]
    public bool useHealthCondition;
    [Tooltip("체력 비율 (0.0 ~ 1.0) 이하일 때 발동")]
    [Range(0f, 1f)] public float healthThreshold = 0.5f;
    
    [Header("시간 조건")]
    public bool useTimeCondition;
    [Tooltip("전투 진입 후 지정된 시간(초)이 경과했을 때 발동")]
    public float timeThreshold = 30f;

    [HideInInspector] public bool hasTriggered = false;
}

/// <summary>
/// PhaseSO를 런타임에서 실행하고 전이(상태) 머신 역할을 수행하는 핵심 컴포넌트.
/// </summary>
public class PhaseRunner : MonoBehaviour
{
    [Header("페이즈(Phase) 상태 설정")]
    [Tooltip("플레이어 감지 전 대기/순찰할 페이즈")]
    [SerializeField] private PhaseSO idlePhase;
    
    [Tooltip("플레이어 감지 시 돌입할 메인 전투 페이즈 (기존 '조절' 필드 대체)")]
    [SerializeField] private PhaseSO combatPhase;
    
    [Tooltip("플레이어가 감지 범위를 벗어나 도망쳤을 때 실행할 복귀 페이즈")]
    [SerializeField] private PhaseSO returnPhase;

    [Header("감지 (Detection) 범위 설정")]
    [Tooltip("씬 뷰에서 이 오브젝트를 선택했을 때 감지 및 도주 범위 기즈모를 표시할지 여부")]
    [SerializeField] private bool showDetectionGizmo = true;
    
    [Tooltip("플레이어를 감지하여 전투 페이즈로 돌입하는 반경")]
    [SerializeField] private float detectionRadius = 10f;
    [Tooltip("전투 중 플레이어가 이 반경을 벗어나면 도망친 것으로 간주하고 복귀 페이즈로 전환")]
    [SerializeField] private float escapeRadius = 15f;

    [Header("조건부 특수 페이즈 (체력/시간)")]
    [SerializeField] private List<ConditionalPhase> conditionalPhases = new List<ConditionalPhase>();

    [Header("실행 설정")]
    [SerializeField] private bool playOnStart = true;

    [Header("참조")]
    [SerializeField] private CapsuleController controller;

    // ── 런타임 상태 ──
    private PhaseSO currentPhase;
    private AIPhaseState currentState = AIPhaseState.None;
    private float combatStartTime = 0f;

    // 외부에서 체력을 연동해 주어야 하는 변수 (기본값 1.0 = 100%)
    public float CurrentHealthRatio { get; set; } = 1f;

    private int lastSelectedIndex = -1;
    public int LastSelectedIndex => lastSelectedIndex;
    private bool isRunning;
    private ActionData currentExecutingAction;
    public ActionData CurrentExecutingAction => currentExecutingAction;

    private InterruptToken currentToken;
    private Transform cachedTarget;
    
    // ── 초기 위치 기록용 ──
    private Transform spawnPointTransform;

    public event Action<PhaseSelectionInfo> OnSequenceSelected;
    private bool hitConfirmed;
    private AttackCaster attackCaster;
    private float FacingSignX => Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, 180f)) < 90f ? 1f : -1f;

    private Vector3 GetOrientedOffset(Vector3 offset)
    {
        return new Vector3(offset.x * FacingSignX, offset.y, offset.z);
    }

    private void Awake()
    {
        if (!controller) controller = GetComponent<CapsuleController>();
        attackCaster = GetComponent<AttackCaster>();
    }

    private void Start()
    {
        // 시작 시 X, Z 위치를 기록하기 위해 투명한 빈 게임오브젝트(가짜 타겟)를 하나 생성합니다.
        GameObject spawnDummy = new GameObject($"{gameObject.name}_SpawnPoint");
        spawnDummy.transform.position = transform.position;
        spawnPointTransform = spawnDummy.transform;

        CacheTarget();
        
        if (playOnStart && !GetComponent<PhaseManager>())
        {
            // 게임 시작 시 기본 대기 상태로 시작
            ChangeState(AIPhaseState.Idle);
        }
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        // 1. 상태 전이용 진짜 플레이어와의 거리를 잰다.
        Transform actualPlayer = null;
        if (combatPhase != null && !string.IsNullOrEmpty(combatPhase.targetTag))
            actualPlayer = CombatTargetRegistry.GetFirst(combatPhase.targetTag);

        float distance = actualPlayer ? Vector3.Distance(transform.position, actualPlayer.position) : float.MaxValue;

        // 2. ActionSequence가 바라볼 가짜/진짜 타겟을 업데이트한다.
        CacheTarget();

        // 상시로 조건부 페이즈 검사
        CheckConditionalPhases();

        // AI 상태에 따른 전이 로직
        switch (currentState)
        {
            case AIPhaseState.Idle:
                if (distance <= detectionRadius)
                    ChangeState(AIPhaseState.Combat);
                break;

            case AIPhaseState.Combat:
                if (distance > escapeRadius)
                    ChangeState(AIPhaseState.Return);
                break;

            case AIPhaseState.Return:
                // 복귀 중이라도 다시 플레이어가 가까이 오면 즉시 전투
                if (distance <= detectionRadius)
                    ChangeState(AIPhaseState.Combat);
                break;

            case AIPhaseState.Conditional:
                // 특수 페이즈는 스스로 끝나기를 기다림 (루프 종료 시 자동 전투 복귀)
                break;
        }
    }

    private void CheckConditionalPhases()
    {
        if (conditionalPhases == null) return;

        foreach (var cp in conditionalPhases)
        {
            if (cp.hasTriggered || cp.phase == null) continue;

            bool triggered = false;

            if (cp.useHealthCondition && CurrentHealthRatio <= cp.healthThreshold)
                triggered = true;

            if (!triggered && cp.useTimeCondition && currentState == AIPhaseState.Combat)
            {
                if (Time.time - combatStartTime >= cp.timeThreshold)
                    triggered = true;
            }

            if (triggered)
            {
                cp.hasTriggered = true;
                ChangeState(AIPhaseState.Conditional, cp.phase);
                return; // 한 번에 하나씩만
            }
        }
    }

    public void ChangeState(AIPhaseState newState, PhaseSO overridePhase = null)
    {
        if (currentState == newState && overridePhase == null) return;

        currentState = newState;
        StopPhase();

        PhaseSO nextPhase = null;
        switch (currentState)
        {
            case AIPhaseState.Idle: 
                nextPhase = idlePhase; 
                break;
            case AIPhaseState.Combat: 
                nextPhase = combatPhase; 
                combatStartTime = Time.time; 
                break;
            case AIPhaseState.Return: 
                nextPhase = returnPhase; 
                break;
            case AIPhaseState.Conditional: 
                nextPhase = overridePhase; 
                break;
        }

        if (nextPhase != null)
        {
            currentPhase = nextPhase;
            lastSelectedIndex = -1;
            CacheTarget(); // 상태 변경 시 타겟 재설정
            StartPhase();
        }
        else
        {
            currentPhase = null;
        }
    }

    // 외부 API
    public void SetPhase(PhaseSO newPhase)
    {
        ChangeState(AIPhaseState.Conditional, newPhase);
    }

    public void StartPhase()
    {
        if (isRunning || currentPhase == null) return;
        StartCoroutine(PhaseLoop());
    }

    public void StopPhase()
    {
        isRunning = false;
        currentToken?.Interrupt(InterruptReason.External);
        StopAllCoroutines();
    }

    public void InterruptCurrentSequence(InterruptReason reason = InterruptReason.External)
    {
        currentToken?.Interrupt(reason);
    }

    private void CacheTarget()
    {
        // **매직 트릭**: 복귀(Return) 상태일 때는 플레이어가 아닌, 시작 시점에 기록해둔 스폰 더미를 타겟으로 둔갑시킵니다.
        if (currentState == AIPhaseState.Return)
        {
            cachedTarget = spawnPointTransform;
            return;
        }

        // 그 외 상태(대기, 전투)일 때는 정상적으로 플레이어를 타겟으로 삼습니다.
        if (combatPhase != null && !string.IsNullOrEmpty(combatPhase.targetTag))
            cachedTarget = CombatTargetRegistry.GetFirst(combatPhase.targetTag);
    }

    private void OnEnable()
    {
        if (attackCaster) attackCaster.OnHitConfirmed += HandleHitConfirmed;
    }

    private void OnDisable()
    {
        if (attackCaster) attackCaster.OnHitConfirmed -= HandleHitConfirmed;
    }

    private void HandleHitConfirmed()
    {
        hitConfirmed = true;
    }

    private IEnumerator PhaseLoop()
    {
        isRunning = true;
        int repeatDone = 0;

        while (isRunning && currentPhase != null && (currentPhase.isInfiniteLoop || repeatDone < currentPhase.repeatCount))
        {
            int selectedIndex = SelectEntryByWeight();
            if (selectedIndex < 0)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            PhaseEntry entry = currentPhase.entries[selectedIndex];
            lastSelectedIndex = selectedIndex;

            yield return StartCoroutine(ExecuteActionSequence(entry.actionSequence));

            if (entry.isComboStarter && entry.comboFollowUps != null && entry.comboFollowUps.Count > 0)
            {
                if (hitConfirmed)
                {
                    for (int c = 0; c < entry.comboFollowUps.Count; c++)
                    {
                        ActionSequenceSO followUp = entry.comboFollowUps[c];
                        if (followUp == null) continue;

                        hitConfirmed = false;
                        yield return StartCoroutine(ExecuteActionSequence(followUp));

                        if (c < entry.comboFollowUps.Count - 1 && !hitConfirmed)
                            break;
                    }
                }
                hitConfirmed = false;
            }
            else
            {
                hitConfirmed = false;
            }

            repeatDone++;
        }

        isRunning = false;

        // 페이즈 반복 완전 종료 후 자동 전이
        if (currentState == AIPhaseState.Return)
        {
            ChangeState(AIPhaseState.Idle);
        }
        else if (currentState == AIPhaseState.Conditional)
        {
            ChangeState(AIPhaseState.Combat);
        }
    }

    private int SelectEntryByWeight()
    {
        if (currentPhase == null || currentPhase.entries == null || currentPhase.entries.Count == 0) return -1;

        Vector3 relPos = GetRelativePositionToPlayer();
        float distanceToPlayer = relPos.magnitude;
        float totalWeight = 0f;

        float[] weights = new float[currentPhase.entries.Count];
        string[] names = new string[currentPhase.entries.Count];

        for (int i = 0; i < currentPhase.entries.Count; i++)
        {
            PhaseEntry entry = currentPhase.entries[i];
            names[i] = entry.actionSequence ? entry.actionSequence.name : "(없음)";
            if (!entry.actionSequence) continue;

            float w = entry.baseWeight;
            w *= entry.EvaluateDistanceMultiplier(relPos, transform.forward);

            if (i == lastSelectedIndex)
                w *= currentPhase.repeatPenalty;

            w = Mathf.Max(0f, w);

            weights[i] = w;
            totalWeight += w;
        }

        if (totalWeight <= 0f) return -1;

        float[] probs = new float[weights.Length];
        for (int i = 0; i < weights.Length; i++)
            probs[i] = weights[i] / totalWeight;

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

    private IEnumerator ExecuteActionSequence(ActionSequenceSO sequence)
    {
        if (!sequence || !controller) yield break;

        currentToken = new InterruptToken();
        var parallelCoroutines = new System.Collections.Generic.List<Coroutine>();

        foreach (var action in sequence.actions)
        {
            if (currentToken.IsInterrupted)
            {
                currentExecutingAction = null;
                yield break;
            }

            currentExecutingAction = action;

            ActionState state = CreateState(action, currentToken);
            if (state == null) continue;

            if (action is AttackActionData)
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

        foreach (var c in parallelCoroutines)
        {
            if (c != null) yield return c;
        }

        currentExecutingAction = null;
    }

    private ActionState CreateState(ActionData action, InterruptToken token)
    {
        return action switch
        {
            MoveActionData move           => new MoveState(controller, move, token, cachedTarget),
            WaitActionData wait           => new WaitState(controller, wait, token, cachedTarget),
            RangedAttackActionData ranged => new RangedAttackState(controller, ranged, token, cachedTarget),
            AttackActionData atk          => new AttackState(controller, atk, token, cachedTarget),
            _                             => null,
        };
    }

    private Vector3 GetRelativePositionToPlayer()
    {
        if (combatPhase == null || string.IsNullOrEmpty(combatPhase.targetTag)) return Vector3.zero;

        if (!cachedTarget)
        {
            CacheTarget();
            if (!cachedTarget) return Vector3.zero;
        }

        Vector3 diff = cachedTarget.position - transform.position;
        diff.y = 0f;
        return diff;
    }

    private static readonly Color[] GizmoColors =
    {
        new Color(1f,  0.3f, 0.3f, 1f),
        new Color(0.3f, 0.7f, 1f,  1f),
        new Color(0.3f, 1f,  0.3f, 1f),
        new Color(1f,  0.85f, 0.1f, 1f),
        new Color(0.8f, 0.3f, 1f,  1f),
        new Color(1f,  0.55f, 0.1f, 1f),
    };

    private void OnDrawGizmosSelected()
    {
        if (showDetectionGizmo)
        {
            // 1. 감지 범위 기즈모
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.3f); // 주황색 반투명
            DrawGizmoCircle(transform.position, detectionRadius);
            
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f); // 회색 반투명
            DrawGizmoCircle(transform.position, escapeRadius);
        }
    }

    private void OnDrawGizmos()
    {
        // 2. 공격 범위 기즈모 (현재 페이즈 우선, 없으면 전투 페이즈)
        PhaseSO phaseToDraw = Application.isPlaying ? currentPhase : combatPhase;
        if (!phaseToDraw) return;

        for (int i = 0; i < phaseToDraw.entries.Count; i++)
        {
            PhaseEntry entry = phaseToDraw.entries[i];
            Color col = GizmoColors[i % GizmoColors.Length];

            if (entry.showGizmos && entry.actionSequence)
            {
                foreach (var action in entry.actionSequence.actions)
                    DrawActionGizmo(action, col);
            }

            if (entry.showRangeGizmo)
                DrawRangeGizmo(entry, col);

            if (entry.comboFollowUps != null)
            {
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
        if (action is not AttackActionData atkData) return;

        if (Application.isPlaying && attackCaster && attackCaster.IsGizmoHighlighted)
        {
            if (currentExecutingAction == action)
                color = new Color(0f, 0.4f, 1f, 1f);
        }

        if (atkData is RangedAttackActionData rangedData)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.8f);
            Vector3 spawnPos = transform.position + GetOrientedOffset(rangedData.attackOffset);
            Gizmos.DrawWireSphere(spawnPos, 0.12f);
            return;
        }

        Vector3 center = atkData.isFixedAttack
            ? atkData.targetPosition
            : transform.position + GetOrientedOffset(atkData.attackOffset);

        Gizmos.color = new Color(color.r, color.g, color.b, 0.85f);
        Color fillColor = new Color(color.r, color.g, color.b, 0.12f);

        switch (atkData.attackShape)
        {
            case AttackShape.Sphere:
                Gizmos.DrawWireSphere(center, atkData.attackRadius);
                Gizmos.color = fillColor;
                Gizmos.DrawSphere(center, atkData.attackRadius);
                break;

            case AttackShape.Box:
                Quaternion boxRot = atkData.isFixedAttack ? Quaternion.identity : Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                Matrix4x4 prev = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(center, boxRot, Vector3.one);
                Gizmos.color = new Color(color.r, color.g, color.b, 0.85f);
                Gizmos.DrawWireCube(Vector3.zero, atkData.attackHitBoxSize);
                Gizmos.color = fillColor;
                Gizmos.DrawCube(Vector3.zero, atkData.attackHitBoxSize);
                Gizmos.matrix = prev;
                break;

            case AttackShape.Cylinder:
                Gizmos.color = new Color(color.r, color.g, color.b, 0.85f);
                DrawGizmoCylinder(center, atkData.attackRadius, atkData.attackHeight);
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

    private void DrawXAxisRangeCorridor(Vector3 origin, float minD, float maxD, float alignT, Color color)
    {
        Gizmos.color = new Color(color.r, color.g, color.b, 0.75f);
        Gizmos.DrawLine(origin + new Vector3(-maxD, 0f, -alignT), origin + new Vector3(+maxD, 0f, -alignT));
        Gizmos.DrawLine(origin + new Vector3(-maxD, 0f, +alignT), origin + new Vector3(+maxD, 0f, +alignT));
        Gizmos.DrawLine(origin + new Vector3(-maxD, 0f, -alignT), origin + new Vector3(-maxD, 0f, +alignT));
        Gizmos.DrawLine(origin + new Vector3(+maxD, 0f, -alignT), origin + new Vector3(+maxD, 0f, +alignT));
        Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);
        Gizmos.DrawLine(origin + new Vector3(-minD, 0f, -alignT), origin + new Vector3(-minD, 0f, +alignT));
        Gizmos.DrawLine(origin + new Vector3(+minD, 0f, -alignT), origin + new Vector3(+minD, 0f, +alignT));
    }

    private void DrawZAxisRangeCorridor(Vector3 origin, float minD, float maxD, float alignT, Color color)
    {
        Gizmos.color = new Color(color.r, color.g, color.b, 0.75f);
        Gizmos.DrawLine(origin + new Vector3(-alignT, 0f, -maxD), origin + new Vector3(-alignT, 0f, +maxD));
        Gizmos.DrawLine(origin + new Vector3(+alignT, 0f, -maxD), origin + new Vector3(+alignT, 0f, +maxD));
        Gizmos.DrawLine(origin + new Vector3(-alignT, 0f, -maxD), origin + new Vector3(+alignT, 0f, -maxD));
        Gizmos.DrawLine(origin + new Vector3(-alignT, 0f, +maxD), origin + new Vector3(+alignT, 0f, +maxD));
        Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);
        Gizmos.DrawLine(origin + new Vector3(-alignT, 0f, -minD), origin + new Vector3(+alignT, 0f, -minD));
        Gizmos.DrawLine(origin + new Vector3(-alignT, 0f, +minD), origin + new Vector3(+alignT, 0f, +minD));
    }
}
