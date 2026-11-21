// Pong — spawns the ball on the SERVER as a NetworkObject (Step 7 of the Week 2 build).
// Put it on the Court object, next to Court.cs, together with a NetworkObject component
// (a NetworkObject that sits in the scene is a "scene object": the server spawns it for
// every client automatically when they join, so OnStartServer runs here once, on the server).
//
// Why a spawner and not Court.Awake(): Awake runs on EVERY machine and gave every client its
// own private ball, which is exactly the Step 5 bug. A networked ball must be created ONCE,
// by the server, and replicated to the clients by FishNet.
using FishNet.Object;
using UnityEngine;

public class BallSpawner : NetworkBehaviour
{
    [SerializeField] private NetworkObject ballPrefab;      // Assets/Prefabs/Ball.prefab (NetworkObject + NetworkBall + NetworkTransform)

    public override void OnStartServer()
    {
        // Runs on the server only, when this scene object is spawned (i.e. when the server starts).
        NetworkObject ball = Instantiate(ballPrefab, new Vector3(0, Court.BallY, 0), Quaternion.identity);
        ball.name = "Ball";
        Spawn(ball.gameObject);                             // NetworkBehaviour.Spawn: server-only; the library tells every client
    }
}
