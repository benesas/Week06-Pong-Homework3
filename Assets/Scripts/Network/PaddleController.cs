using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

public class PaddleController : NetworkBehaviour
{
    public const int Left = 0, Right = 1, Spectator = 2;

    public static PaddleController Local { get; private set; }

    public static readonly List<PaddleController> All = new List<PaddleController>();

    private static readonly Dictionary<int, PaddleController> ByOwner = new Dictionary<int, PaddleController>();

    [SerializeField] private float speed      = 8f;
    [SerializeField] private Color leftColor  = new Color(0.35f, 0.65f, 1f);
    [SerializeField] private Color rightColor = new Color(1f, 0.60f, 0.25f);

    public readonly SyncVar<int> Side = new SyncVar<int>(Spectator);

    private void Awake()
    {
        Side.OnChange += (prev, next, asServer) => Place();
    }

    public override void OnStartServer()
    {

        if (!All.Contains(this)) All.Add(this);
        ByOwner[OwnerId] = this;

        bool leftTaken = false, rightTaken = false;
        foreach (PaddleController p in All)
        {
            if (p == this) continue;
            if (p.Side.Value == Left)  leftTaken  = true;
            if (p.Side.Value == Right) rightTaken = true;
        }
        Side.Value = !leftTaken ? Left : (!rightTaken ? Right : Spectator);

        Debug.Log($"[Server] Spawned paddle #{ObjectId} for client {OwnerId}: {SideName(Side.Value)}.");
        Debug.Log($"[Server] Paddles known here: {All.Count}.");
        GameState.Announce($"Player {OwnerId} joined as {SideName(Side.Value)}");
        GameState.Instance?.ServerPlayersChanged();
    }

    public override void OnStopServer()
    {
        All.Remove(this);
        ByOwner.Remove(OwnerId);
        GameState.Announce($"Player {OwnerId} ({SideName(Side.Value)}) left");
        GameState.Instance?.ServerPlayersChanged();
    }

    public override void OnStartClient()
    {

        if (!All.Contains(this)) All.Add(this);
        bool mine = IsOwner;
        gameObject.name = mine ? $"Paddle {OwnerId} (me)" : $"Paddle {OwnerId}";
        if (mine)
        {
            Local = this;
            TimeManager.OnTick += OwnerTick;
            Debug.Log($"[Client] I am client {OwnerId}, playing {SideName(Side.Value)}. My paddle is #{ObjectId}. Only I read the keyboard for it.");
        }
        else
        {
            Debug.Log($"[Client] Another paddle appeared: client {OwnerId}, {SideName(Side.Value)}, object #{ObjectId}. NetworkTransform moves it for me.");
        }
        Place();
    }

    public override void OnStopClient()
    {
        All.Remove(this);
        if (Local == this) { TimeManager.OnTick -= OwnerTick; Local = null; }
        Debug.Log($"[Client] Paddle #{ObjectId} gone. Paddles known here: {All.Count}.");
    }

    public static PaddleController ForConnection(NetworkConnection conn)
    {
        if (conn == null) return null;
        ByOwner.TryGetValue(conn.ClientId, out PaddleController p);
        return p;
    }

    public static PaddleController ForSide(int side)
    {
        foreach (PaddleController p in All) if (p != null && p.Side.Value == side) return p;
        return null;
    }

    public static string SideName(int side) => side == Left ? "LEFT" : side == Right ? "RIGHT" : "SPECTATOR";

    private const double MinInputGap = 0.030;
    private const double MinServeGap = 1.0;
    private double _lastInputTime = -1;
    private double _lastServeTime = -1;
    private float _direction;
    private bool _burstRequested, _oversizedRequested;

    private void Update()
    {
        if (!IsOwner || Side.Value == Spectator) return;
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        float dir = 0;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir += 1;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir -= 1;

        _direction = dir;
        if (kb.cKey.wasPressedThisFrame) _burstRequested = true;
        if (kb.xKey.wasPressedThisFrame) _oversizedRequested = true;

        if (kb.tKey.wasPressedThisFrame && NetworkBall.Instance != null)
        {
            Vector3 pos = transform.position;
            pos.z = NetworkBall.Instance.transform.position.z;
            transform.position = pos;
        }
        if (kb.gKey.wasPressedThisFrame && GameState.Instance != null)
            GameState.Instance.LeftScore.Value = 9;
        if (kb.spaceKey.wasPressedThisFrame) ServerRequestServe();
    }

    private void OwnerTick()
    {
        if (!IsOwner || Side.Value == Spectator) return;
        if (_burstRequested)
        {
            for (int i = 0; i < 20; i++) ServerMove(1f);
            _burstRequested = false;
            _oversizedRequested = false;
        }
        else if (_oversizedRequested)
        {
            ServerMove(100f);
            _oversizedRequested = false;
        }
        else ServerMove(_direction);
    }

    [ServerRpc]
    private void ServerMove(float direction)
    {

        if (float.IsNaN(direction) || float.IsInfinity(direction)) return;
        double now = TimeManager.TicksToTime(TimeManager.Tick);
        if (_lastInputTime >= 0 && now - _lastInputTime < MinInputGap)
        {
            Debug.Log($"[Server] Rejected input from {OwnerId}: too fast");
            return;
        }
        _lastInputTime = now;
        if (Side.Value == Spectator) return;
        direction = Mathf.Clamp(direction, -1f, 1f);
        transform.position += Vector3.forward * direction * speed * (float)TimeManager.TickDelta;
        Court.ClampPaddle(transform);

        if (TimeManager.Tick % 10 == 0)
            GetComponent<FishNet.Component.Transforming.NetworkTransform>().ForceSend();
    }

    [ServerRpc]
    private void ServerRequestServe()
    {
        GameState gs = GameState.Instance;
        if (gs == null || NetworkBall.Instance == null) return;
        double now = TimeManager.TicksToTime(TimeManager.Tick);
        if (_lastServeTime >= 0 && now - _lastServeTime < MinServeGap)
        {
            TargetRefused(Owner, "TOO FAST");
            return;
        }
        _lastServeTime = now;
        if (Side.Value == Spectator) return;
        if (gs.ServingSide.Value != Side.Value) return;
        if (NetworkBall.Instance.InPlay.Value) return;

        NetworkBall.Instance.ServerServe(towardSide: 1 - Side.Value);
        GameState.Announce($"Player {OwnerId} ({SideName(Side.Value)}) served");
    }

    [TargetRpc]
    private void TargetRefused(NetworkConnection target, string why)
    {
        ConnectionHud.Flash(why);
    }

    [ObserversRpc]
    public void ObserversScored(int left, int right)
    {
        Debug.Log($"[Client] Goal! Player {OwnerId} ({SideName(Side.Value)}) scored. {left} : {right}");
        ConnectionHud.Flash($"{SideName(Side.Value)} scored!  {left} : {right}");
    }

    [TargetRpc]
    public void TargetYouServe(NetworkConnection target)
    {
        Debug.Log("[Client] YOU serve. Press Space.");
        ConnectionHud.Flash("YOU SERVE — press Space");
    }

    private void Place()
    {
        int side = Side.Value;
        Renderer r = GetComponent<Renderer>();
        if (side == Spectator)
        {
            if (r != null) r.enabled = false;
            transform.position = new Vector3(0, -5f, 0);
            return;
        }
        if (r != null) r.enabled = true;
        transform.position = new Vector3(side == Left ? -Court.PaddleX : Court.PaddleX, 0.5f, transform.position.z);
        Court.Paint(gameObject, side == Left ? leftColor : rightColor);
    }
}
