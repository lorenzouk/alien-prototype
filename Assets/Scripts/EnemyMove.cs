using UnityEngine;

public class EnemyMove : MonoBehaviour
{
    [SerializeField] float variance = 10.0f;
    [SerializeField] float moveDuration = 1f;
    [SerializeField] float waitDuration = 0.5f;

    float waitTimer = 0.0f;
    bool isMoving = false;
    Vector2 origin;
    Vector2 startPosition;
    Vector2 targetPosition;
    float elapsedMoveTime = 0.0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        origin = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        //if not moving, wait
        if (!isMoving)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer > waitDuration)
            {
                waitTimer = 0.0f;

                startPosition = transform.position;
                //set target before move is called
                targetPosition = origin + new Vector2(Random.Range(-variance, variance), Random.Range(-variance, variance));

                isMoving = true;
            }
        } else
        {
            Move();
        }
    }

    void Move()
    {
        float t = elapsedMoveTime / moveDuration;

        transform.position = Vector2.Lerp(startPosition, targetPosition, t);

        elapsedMoveTime += Time.deltaTime;
        if (elapsedMoveTime >= moveDuration)
        {
            elapsedMoveTime = 0.0f;

            isMoving = false;
        }
    }
}
