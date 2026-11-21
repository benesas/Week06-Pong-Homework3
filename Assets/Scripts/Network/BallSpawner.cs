using FishNet.Object;
using UnityEngine;

public class BallSpawner : NetworkBehaviour
{
    [SerializeField] private NetworkObject ballPrefab;

    public override void OnStartServer()
    {

        NetworkObject ball = Instantiate(ballPrefab, new Vector3(0, Court.BallY, 0), Quaternion.identity);
        ball.name = "Ball";
        Spawn(ball.gameObject);
    }
}
