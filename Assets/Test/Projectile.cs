using UnityEngine;

public class Projectile : MonoBehaviour
{
    private ActionData data;
    private Vector3 moveDirection;
    private LayerMask targetLayer;
    private GameObject attacker;
    private AttackCaster attackCaster;

    public void Initialize(ActionData attackData, Vector3 direction, LayerMask layer, GameObject attackerObj)
    {
        data = attackData;
        moveDirection = direction.normalized;
        targetLayer = layer;
        attacker = attackerObj;
        if (attacker) attackCaster = attacker.GetComponent<AttackCaster>();

        if (moveDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }

        Destroy(gameObject, data.projectileLifeTime);
    }

    private void Update()
    {
        if (data == null) return;
        transform.position += moveDirection * data.projectileSpeed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 발사자 본인 또는 자식 오브젝트에 맞고 증발하는 현상 방지
        if (attacker != null)
        {
            if (other.gameObject == attacker || other.transform.IsChildOf(attacker.transform)) return;
        }

        if (((1 << other.gameObject.layer) & targetLayer) == 0) return;

        // ── IHittable 인터페이스 호출 ──────────────────────────
        IHittable hittable = other.GetComponentInParent<IHittable>();
        if (hittable != null)
        {
            Vector3 hitDir   = (other.transform.position - transform.position).normalized;
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            CombatHitData hitData  = CombatHitData.Create(data, hitPoint, hitDir, transform);

            bool applied = hittable.OnHit(hitData);
            if (applied)
            {
                CombatEventBus.Instance.RaiseHitDealt(hitData);
                if (attackCaster) attackCaster.NotifyRangedHit();
            }
        }
        else
        {
            // IHittable 미구현 — 폴백 로그
            Debug.Log($"[Projectile] {other.name} 명중 (IHittable 미구현 — 폴백)");
            if (attackCaster) attackCaster.NotifyRangedHit();
        }

        Destroy(gameObject);
    }
}