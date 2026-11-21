// Pong — the networked ball (Step 7 of the Week 2 build). Replaces Starter/LocalBall.cs.
//
// The ball is the object nobody owns. Step 5 showed three copies of the game with three balls
// playing three different matches. This one is a NetworkObject spawned by the SERVER, moved
// by the SERVER only, and replicated to every client by a NetworkTransform that is NOT
// client-authoritative. Clients never touch its position; they receive it, a tick late,
// and interpolate. Turn the tick rate down in Step 10 and watch this object step.
//
// Nothing here runs on a client except OnChange (repaint) — read the first line of Update.
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class NetworkBall : NetworkBehaviour
{
    public static NetworkBall Instance { get; private set; }

    [SerializeField] private float speed = 7f;

    // SyncVar<T>: is the ball moving? Server writes; every client repaints from OnChange.
    public readonly SyncVar<bool> InPlay = new SyncVar<bool>();

    private Vector3 _velocity;                 // server-only state: never replicated, never needed on a client

    private void Awake()
    {
        Instance = this;
        InPlay.OnChange += (prev, next, asServer) => Court.Paint(gameObject, ColorFor(next));
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// White while a rally runs, grey while the ball waits for a serve.
    private static Color ColorFor(bool inPlay) => inPlay ? Color.white : new Color(0.5f, 0.5f, 0.55f);

    public override void OnStartClient()
    {
        // OnChange fires only on a CHANGE. A client that joins while the ball is waiting (InPlay false since spawn)
        // never gets one, so paint the initial state here or the ball looks "in play" (white) before anyone served.
        Court.Paint(gameObject, ColorFor(InPlay.Value));
    }

    public override void OnStartServer()
    {
        transform.position = new Vector3(0, Court.BallY, 0);
        Debug.Log("[Server] Ball spawned at the centre. It moves on the server only; clients receive its position.");
    }

    /// Server-only. Called from PaddleController.ServerRequestServe after the server has validated the request.
    public void ServerServe(int towardSide)
    {
        if (!IsServerInitialized || InPlay.Value) return;
        float dx = towardSide == PaddleController.Left ? -1f : 1f;
        float dz = Random.Range(-0.6f, 0.6f);
        _velocity = new Vector3(dx, 0, dz).normalized * speed;
        InPlay.Value = true;                    // the ONE write that starts a rally; every client repaints the ball white
    }

    private void Update()
    {
        if (!IsServerInitialized) return;       // THE line: the ball moves on the server only. A client's Update does nothing.
        if (!InPlay.Value) return;

        Vector3 p = transform.position + _velocity * Time.deltaTime;

        // walls
        float zLimit = Court.HalfHeight - Court.BallRadius;
        if (Mathf.Abs(p.z) > zLimit) { p.z = Mathf.Sign(p.z) * zLimit; _velocity.z = -_velocity.z; }

        // paddles: the server's copies, where NetworkTransform put them (the owner told us; Week 3 asks whether to believe it)
        foreach (PaddleController paddle in PaddleController.All)
        {
            if (paddle == null || paddle.Side.Value == PaddleController.Spectator) continue;
            Vector3 pp = paddle.transform.position;
            bool towards = (pp.x < 0 && _velocity.x < 0) || (pp.x > 0 && _velocity.x > 0);
            if (!towards) continue;
            if (Mathf.Abs(p.x - pp.x) < 0.5f && Mathf.Abs(p.z - pp.z) < Court.PaddleHalfLength + Court.BallRadius)
            {
                _velocity.x = -_velocity.x;
                _velocity.z += (p.z - pp.z) * 3f;              // hit near the end of the paddle: more angle
                _velocity = _velocity.normalized * speed;
                p.x = pp.x + (pp.x < 0 ? 0.5f : -0.5f);
            }
        }

        transform.position = p;                 // NetworkTransform sees the change and sends it this tick

        // goals: decided here, announced by GameState
        if (p.x < -Court.HalfWidth - 0.5f)      ServerGoal(scorerSide: PaddleController.Right);
        else if (p.x > Court.HalfWidth + 0.5f)  ServerGoal(scorerSide: PaddleController.Left);
    }

    private void ServerGoal(int scorerSide)
    {
        ServerReset();
        GameState.Instance?.ServerGoal(scorerSide);
    }

    /// Server-only. Stops the ball and puts it back in the centre without scoring.
    /// GameState calls it when a player leaves mid-rally or when the court empties.
    public void ServerReset()
    {
        if (!IsServerInitialized) return;
        InPlay.Value = false;
        _velocity = Vector3.zero;
        transform.position = new Vector3(0, Court.BallY, 0);
    }
}
