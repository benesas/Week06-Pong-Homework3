using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class NetworkBall : NetworkBehaviour
{
    public static NetworkBall Instance { get; private set; }

    [SerializeField] private float speed = 7f;

    public readonly SyncVar<bool> InPlay = new SyncVar<bool>();

    private Vector3 _velocity;

    private void Awake()
    {
        Instance = this;
        InPlay.OnChange += (prev, next, asServer) => Court.Paint(gameObject, ColorFor(next));
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private static Color ColorFor(bool inPlay) => inPlay ? Color.white : new Color(0.5f, 0.5f, 0.55f);

    public override void OnStartClient()
    {

        Court.Paint(gameObject, ColorFor(InPlay.Value));
    }

    public override void OnStartServer()
    {
        transform.position = new Vector3(0, Court.BallY, 0);
        Debug.Log("[Server] Ball spawned at the centre. It moves on the server only; clients receive its position.");
    }

    public void ServerServe(int towardSide)
    {
        if (!IsServerInitialized || InPlay.Value) return;
        float dx = towardSide == PaddleController.Left ? -1f : 1f;
        float dz = Random.Range(-0.6f, 0.6f);
        _velocity = new Vector3(dx, 0, dz).normalized * speed;
        InPlay.Value = true;
    }

    private void Update()
    {
        if (!IsServerInitialized) return;
        if (!InPlay.Value) return;

        Vector3 p = transform.position + _velocity * Time.deltaTime;

        float zLimit = Court.HalfHeight - Court.BallRadius;
        if (Mathf.Abs(p.z) > zLimit) { p.z = Mathf.Sign(p.z) * zLimit; _velocity.z = -_velocity.z; }

        foreach (PaddleController paddle in PaddleController.All)
        {
            if (paddle == null || paddle.Side.Value == PaddleController.Spectator) continue;
            Vector3 pp = paddle.transform.position;
            bool towards = (pp.x < 0 && _velocity.x < 0) || (pp.x > 0 && _velocity.x > 0);
            if (!towards) continue;
            if (Mathf.Abs(p.x - pp.x) < 0.5f && Mathf.Abs(p.z - pp.z) < Court.PaddleHalfLength + Court.BallRadius)
            {
                _velocity.x = -_velocity.x;
                _velocity.z += (p.z - pp.z) * 3f;
                _velocity = _velocity.normalized * speed;
                p.x = pp.x + (pp.x < 0 ? 0.5f : -0.5f);
            }
        }

        transform.position = p;

        if (p.x < -Court.HalfWidth - 0.5f)      ServerGoal(scorerSide: PaddleController.Right);
        else if (p.x > Court.HalfWidth + 0.5f)  ServerGoal(scorerSide: PaddleController.Left);
    }

    private void ServerGoal(int scorerSide)
    {
        ServerReset();
        GameState.Instance?.ServerGoal(scorerSide);
    }

    public void ServerReset()
    {
        if (!IsServerInitialized) return;
        InPlay.Value = false;
        _velocity = Vector3.zero;
        transform.position = new Vector3(0, Court.BallY, 0);
    }
}
