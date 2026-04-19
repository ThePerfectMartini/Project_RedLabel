using UnityEngine;

public class Portal : MonoBehaviour
{
    [Header("순간이동 할 목적지")]
    public Transform destination; // 인스펙터 창에서 목적지를 지정할 변수

    // 3D 게임일 경우 작동하는 트리거 감지 함수
    private void OnTriggerEnter(Collider other)
    {
        // 포탈에 닿은 오브젝트의 태그가 "Player"인지 확인합니다.
        if (other.CompareTag("Player"))
        {
            // 플레이어의 위치를 목적지의 위치로 즉시 변경합니다.
            other.transform.position = destination.position;
            
            Debug.Log("순간이동 완료!");
        }
    }

    // ---------------------------------------------------------
    // 만약 2D 게임을 만들고 계시다면, 위의 OnTriggerEnter 대신 
    // 아래의 OnTriggerEnter2D 함수를 사용하셔야 합니다!
    // ---------------------------------------------------------
    /*
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.position = destination.position;
        }
    }
    */
}