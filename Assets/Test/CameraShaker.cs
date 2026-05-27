using System.Collections;
using UnityEngine;

/// <summary>
/// CombatEventBus 의 전투 이벤트를 구독해 카메라 흔들림 연출을 수행하는 컴포넌트.
/// Perlin Noise 기반으로 자연스러운 흔들림을 구현합니다.
///
/// 부착 방법:
///   카메라 오브젝트 또는 카메라의 부모 오브젝트에 부착합니다.
///   target 에 카메라(또는 진동시킬 Transform)를 지정하세요.
///   지정하지 않으면 Camera.main 을 자동으로 탐색합니다.
/// </summary>
public class CameraShaker : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // 인스펙터 설정
    // ═══════════════════════════════════════════════════════════

    [Header("타겟")]
    [Tooltip("흔들 카메라 Transform. 비워두면 Camera.main 을 자동 사용합니다.")]
    public Transform target;

    [Header("이벤트별 진폭·시간 프리셋")]
    public ShakePreset onHitPreset    = new ShakePreset { amplitude = 0.08f, duration = 0.15f, frequency = 25f };
    public ShakePreset onParryPreset  = new ShakePreset { amplitude = 0.12f, duration = 0.20f, frequency = 30f };
    public ShakePreset onGroggyPreset = new ShakePreset { amplitude = 0.20f, duration = 0.30f, frequency = 20f };
    public ShakePreset onCounterPreset = new ShakePreset { amplitude = 0.25f, duration = 0.35f, frequency = 22f };
    public ShakePreset onDodgePreset  = new ShakePreset { amplitude = 0.04f, duration = 0.08f, frequency = 40f };

    // ═══════════════════════════════════════════════════════════
    // 내부 상태
    // ═══════════════════════════════════════════════════════════

    private Vector3 originalLocalPos;
    private bool    isShaking;
    private float   currentAmplitude;
    private float   currentDuration;
    private Coroutine shakeRoutine;

    // ═══════════════════════════════════════════════════════════
    // 초기화
    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        if (!target && Camera.main)
            target = Camera.main.transform;

        if (target)
            originalLocalPos = target.localPosition;
    }

    // ═══════════════════════════════════════════════════════════
    // 구독 관리
    // ═══════════════════════════════════════════════════════════

    private void OnEnable()
    {
        CombatEventBus.Instance.OnHitDealt    += HandleHit;
        CombatEventBus.Instance.OnParrySuccess += HandleParry;
        CombatEventBus.Instance.OnGroggyEnter += HandleGroggy;
        CombatEventBus.Instance.OnCounterHit  += HandleCounter;
        CombatEventBus.Instance.OnDodgeSuccess += HandleDodge;
    }

    private void OnDisable()
    {
        if (!CombatEventBus.Instance) return;
        CombatEventBus.Instance.OnHitDealt    -= HandleHit;
        CombatEventBus.Instance.OnParrySuccess -= HandleParry;
        CombatEventBus.Instance.OnGroggyEnter -= HandleGroggy;
        CombatEventBus.Instance.OnCounterHit  -= HandleCounter;
        CombatEventBus.Instance.OnDodgeSuccess -= HandleDodge;
    }

    // ═══════════════════════════════════════════════════════════
    // 이벤트 핸들러
    // ═══════════════════════════════════════════════════════════

    private void HandleHit(CombatHitData _)     => RequestShake(onHitPreset);
    private void HandleParry(CombatHitData _)   => RequestShake(onParryPreset);
    private void HandleGroggy(Transform _)       => RequestShake(onGroggyPreset);
    private void HandleCounter(Transform _)      => RequestShake(onCounterPreset);
    private void HandleDodge(Transform _)        => RequestShake(onDodgePreset);

    // ═══════════════════════════════════════════════════════════
    // 쉐이크 실행
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 외부에서 직접 흔들림을 요청합니다. 이미 실행 중이면 더 강한 진폭으로 교체합니다.
    /// </summary>
    public void RequestShake(ShakePreset preset)
    {
        if (!target) return;

        // 진행 중보다 더 강한 요청이면 교체
        if (isShaking && preset.amplitude <= currentAmplitude) return;

        currentAmplitude = preset.amplitude;
        currentDuration  = preset.duration;

        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(preset));
    }

    private IEnumerator ShakeRoutine(ShakePreset preset)
    {
        isShaking = true;

        // Perlin Noise 오프셋 (매 쉐이크마다 다른 패턴)
        float seed = Random.Range(0f, 100f);
        float elapsed = 0f;

        while (elapsed < preset.duration)
        {
            float t = elapsed / preset.duration;

            // 진폭 감쇠 (후반부로 갈수록 약해짐 — EaseOut)
            float attenuation = 1f - Mathf.Pow(t, 2f);
            float amp = preset.amplitude * attenuation;

            // Perlin Noise 기반 오프셋
            float nx = (Mathf.PerlinNoise(seed + elapsed * preset.frequency, 0f) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(0f, seed + elapsed * preset.frequency) - 0.5f) * 2f;

            target.localPosition = originalLocalPos + new Vector3(nx * amp, ny * amp, 0f);

            // unscaledDeltaTime 사용 — 히트스톱/슬로우 중에도 쉐이크 진행
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 원래 위치 복구
        target.localPosition = originalLocalPos;
        isShaking = false;
        currentAmplitude = 0f;
    }
}

/// <summary>
/// 카메라 쉐이크 강도 프리셋.
/// </summary>
[System.Serializable]
public struct ShakePreset
{
    [Tooltip("진폭 (최대 이동 거리, 유닛)")]
    public float amplitude;

    [Tooltip("지속 시간 (초, 실제 시간 기준)")]
    public float duration;

    [Tooltip("Perlin Noise 주파수 — 높을수록 고주파(잔떨림), 낮을수록 저주파(큰 흔들림)")]
    public float frequency;
}
