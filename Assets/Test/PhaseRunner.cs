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

    /// <summary>
    /// 패턴이 선택될 때마다 발행되는 이벤트 (디버그 모니터 연동용)
    /// </summary>
    public event Action<PhaseSelectionInfo> OnSequenceSelected;
    
    // ── 콤보 판정용 ──
    // AttackCaster의 타격 성공 이벤트를 구독하여 이 플래그를 세팅
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
        if (playOnStart && phase)
            StartPhase();
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
        StopAllCoroutines();
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

        foreach (var action in sequence.actions)
        {
            ActionState state = CreateState(action);
            if (state == null) continue;

            // 공격 액션이면 hitConfirmed를 리셋해서 이번 공격의 성공 여부를 측정
            bool isAttackAction = action.actionType == ActionType.VariableAttack
                || action.actionType == ActionType.FixedAttack
                || action.actionType == ActionType.RangedAttack;

            if (isAttackAction)
                hitConfirmed = false;

            if (action.executeParallel)
            {
                controller.StartCoroutine(state.Execute());
            }
            else
            {
                yield return controller.StartCoroutine(state.Execute());
            }
        }
    }

    private ActionState CreateState(ActionData action)
    {
        switch (action.actionType)
        {
            case ActionType.Move: return new MoveState(controller, action);
            case ActionType.Wait: return new WaitState(controller, action);
            case ActionType.VariableAttack:
            case ActionType.FixedAttack: return new AttackState(controller, action);
            case ActionType.RangedAttack: return new RangedAttackState(controller, action);
            default: return null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 유틸
    // ═══════════════════════════════════════════════════════════
    private Vector3 GetRelativePositionToPlayer()
    {
        if (string.IsNullOrEmpty(phase.targetTag)) return Vector3.zero;

        GameObject playerObj = GameObject.FindWithTag(phase.targetTag);
        if (!playerObj) return Vector3.zero;

        Vector3 diff = playerObj.transform.position - transform.position;
        diff.y = 0f; // Y축 제외, 수평 벡터만 반환
        return diff;
    }
}
