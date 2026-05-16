using UnityEngine;

public class Projectile : MonoBehaviour
{
    private RangedAttackAction data;
    private Vector3 moveDirection;
    private LayerMask targetLayer;
    private GameObject attacker;

    public void Initialize(RangedAttackAction attackData, Vector3 direction, LayerMask layer, GameObject attackerObj)
    {
        data = attackData;
        moveDirection = direction.normalized;
        targetLayer = layer;
        attacker = attackerObj;

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
        // ▼ 버그 수정 1: 발사자 본인 또는 발사자의 자식 오브젝트(무기 등)에 맞고 증발하는 현상 방지
        if (attacker != null)
        {
            if (other.gameObject == attacker || other.transform.IsChildOf(attacker.transform)) return;
        }

        if (((1 << other.gameObject.layer) & targetLayer) != 0)
        {
            Debug.Log($"[Projectile] {other.name} 명중! (데미지: {data.damage})");

            AnimationController targetAnimCtrl = other.GetComponentInChildren<AnimationController>();
            if (targetAnimCtrl != null) targetAnimCtrl.Play("Hit");
            else
            {
                Animator targetAnim = other.GetComponentInChildren<Animator>();
                if (targetAnim != null) targetAnim.Play("Hit");
            }

            Rigidbody targetRb = other.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                Vector3 knockbackDir = (other.transform.position - transform.position).normalized;
                Vector3 finalKnockback = new Vector3(
                    knockbackDir.x * data.knockbackForce.x,
                    data.knockbackForce.y,
                    knockbackDir.z * data.knockbackForce.z
                );

                targetRb.linearVelocity = Vector3.zero;
                targetRb.AddForce(finalKnockback, ForceMode.Impulse);
            }

            Destroy(gameObject);
        }
    }
}