using System;
using UnityEngine;

/// <summary>
/// 전투 이벤트를 발행/구독하는 MonoBehaviour 싱글톤 이벤트 버스.
/// 씬 내 단 하나만 존재하며, VFX · SFX · 히트스톱 · 카메라 쉐이크 등
/// 연출 컴포넌트들이 이 버스를 구독합니다.
///
/// 사용법:
///   구독: CombatEventBus.Instance.OnHitDealt += MyHandler;
///   해제: CombatEventBus.Instance.OnHitDealt -= MyHandler;
///   발행: CombatEventBus.Instance.RaiseHitDealt(hitData);
/// </summary>
public class CombatEventBus : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // 싱글톤
    // ═══════════════════════════════════════════════════════════

    private static CombatEventBus _instance;

    public static CombatEventBus Instance
    {
        get
        {
            if (_instance) return _instance;

            // 씬에 없으면 자동 생성
            GameObject go = new GameObject("[CombatEventBus]");
            _instance = go.AddComponent<CombatEventBus>();
            DontDestroyOnLoad(go);
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ═══════════════════════════════════════════════════════════
    // 이벤트 정의
    // ═══════════════════════════════════════════════════════════

    // ── 타격 이벤트 ───────────────────────────────────────────
    /// <summary>공격이 실제로 적용되었을 때 (공격자 기준). HitStopHandler, CameraShaker 등이 구독.</summary>
    public event Action<CombatHitData> OnHitDealt;

    /// <summary>피격이 실제로 적용되었을 때 (피격자 기준). HitEffectHandler, 피격 SFX 등이 구독.</summary>
    public event Action<CombatHitData> OnHitReceived;

    // ── 전투 상호작용 이벤트 (⑦⑧⑨에서 활성화) ───────────────
    /// <summary>패링(저스트 가드) 성공 시. 슬로우모션 · 패링 이펙트 등이 구독.</summary>
    public event Action<CombatHitData> OnParrySuccess;

    /// <summary>회피(저스트 회피 포함) 성공 시. 슬로우모션 · 회피 이펙트 등이 구독.</summary>
    public event Action<Transform> OnDodgeSuccess;

    /// <summary>적이 그로기 상태 진입 시. UI 게이지 · 그로기 이펙트 등이 구독.</summary>
    public event Action<Transform> OnGroggyEnter;

    /// <summary>그로기 카운터 어택 성공 시. 특수 이펙트 · 슬로우모션 등이 구독.</summary>
    public event Action<Transform> OnCounterHit;

    // ═══════════════════════════════════════════════════════════
    // 발행 메서드
    // ═══════════════════════════════════════════════════════════

    public void RaiseHitDealt(CombatHitData data)    => OnHitDealt?.Invoke(data);
    public void RaiseHitReceived(CombatHitData data) => OnHitReceived?.Invoke(data);
    public void RaiseParry(CombatHitData data)       => OnParrySuccess?.Invoke(data);
    public void RaiseDodge(Transform dodger)         => OnDodgeSuccess?.Invoke(dodger);
    public void RaiseGroggy(Transform target)        => OnGroggyEnter?.Invoke(target);
    public void RaiseCounter(Transform target)       => OnCounterHit?.Invoke(target);

    // ═══════════════════════════════════════════════════════════
    // 씬 전환 정리
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 씬 전환 시 모든 이벤트 구독을 해제합니다.
    /// MonoBehaviour 구독자는 OnDisable/OnDestroy에서 직접 해제하는 것을 권장합니다.
    /// </summary>
    public void ClearAllListeners()
    {
        OnHitDealt     = null;
        OnHitReceived  = null;
        OnParrySuccess = null;
        OnDodgeSuccess = null;
        OnGroggyEnter  = null;
        OnCounterHit   = null;
    }
}
