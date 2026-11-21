// Pong — the networked paddle, Week 2 version (Steps 4, 6 and 8 of the build).
// Step 4: a NetworkBehaviour; only the OWNER reads the keyboard; the SERVER assigns a side.
// Week 06: NetworkTransform is server authoritative; ServerMove validates owner input.
// Step 8: the serve and the goal as three RPCs, one per direction:
//     ServerRpc      owner  -> server   "I pressed Space, let me serve"
//     ObserversRpc   server -> everyone "Player 2 scored, 3 : 2" (an event, not state)
//     TargetRpc      server -> one client "YOU serve now"
// Rule of the week: clients ASK, the server DECIDES, SyncVars carry the RESULT.
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

public class PaddleController : NetworkBehaviour
{
    public const int Left = 0, Right = 1, Spectator = 2;

    /// The paddle this machine controls (null on a headless server, or before spawn).
    public static PaddleController Local { get; private set; }

    /// Every paddle this process knows about, in spawn order (server: all; client: all it observes).
    public static readonly List<PaddleController> All = new List<PaddleController>();

    private static readonly Dictionary<int, PaddleController> ByOwner = new Dictionary<int, PaddleController>();   // server-side lookup

    [SerializeField] private float speed      = 8f;
    [SerializeField] private Color leftColor  = new Color(0.35f, 0.65f, 1f);
    [SerializeField] private Color rightColor = new Color(1f, 0.60f, 0.25f);

    // ---- replicated state: server writes, everyone reads ----
    // Which side of the court this paddle plays on. Decided by the SERVER when the object spawns;
    // a third and later client becomes a spectator (hidden paddle, no input, sees everything).
    public readonly SyncVar<int> Side = new SyncVar<int>(Spectator);

    private void Awake()
    {
        Side.OnChange += (prev, next, asServer) => Place();
    }

    // ---- lifecycle callbacks (FishNet calls these; you never call them yourself) ----

    public override void OnStartServer()
    {
        // Runs on the SERVER when this object is spawned. OwnerId is the connection that owns it.
        if (!All.Contains(this)) All.Add(this);         // a host runs OnStartServer AND OnStartClient on the same object: list it once
        ByOwner[OwnerId] = this;

        // The server decides the side: first free of Left, Right; otherwise Spectator.
        bool leftTaken = false, rightTaken = false;
        foreach (PaddleController p in All)
        {
            if (p == this) continue;
            if (p.Side.Value == Left)  leftTaken  = true;
            if (p.Side.Value == Right) rightTaken = true;
        }
        Side.Value = !leftTaken ? Left : (!rightTaken ? Right : Spectator);    // SyncVar write on the server: every client will place it

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
        // Runs on EVERY client that can see this object — the owner and the others.
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

    // ---- server-side helpers ----

    /// Server-only: the paddle owned by a connection (null if none).
    public static PaddleController ForConnection(NetworkConnection conn)
    {
        if (conn == null) return null;
        ByOwner.TryGetValue(conn.ClientId, out PaddleController p);
        return p;
    }

    /// Any machine: the paddle playing a side (null if that side is empty).
    public static PaddleController ForSide(int side)
    {
        foreach (PaddleController p in All) if (p != null && p.Side.Value == side) return p;
        return null;
    }

    public static string SideName(int side) => side == Left ? "LEFT" : side == Right ? "RIGHT" : "SPECTATOR";

    // Week 06: istemci konum değil, yön gönderir. Kararı sunucu verir.
    private const double MinInputGap = 0.030;
    private const double MinServeGap = 1.0;
    private double _lastInputTime = -1;
    private double _lastServeTime = -1; // static değil: her raketin kendi süresi
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

        // T ve G özellikle RPC KULLANMAZ: host güven sınırı deneyi.
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

    // Her ağ tickinde tek normal istek: FPS hareket hızını değiştirmez.
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
        // Sıra: biçim -> hız sınırı -> oyun kuralı -> durum değişikliği.
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
    }

    // ---- the serve and the goal: three RPCs, three directions ----

    [ServerRpc]                                         // owner -> server. Only the owner may call it (default RequireOwnership = true).
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
        if (gs.ServingSide.Value != Side.Value) return;         // not your serve: the request is refused, nothing happens anywhere
        if (NetworkBall.Instance.InPlay.Value) return;          // stale: the ball is already moving

        NetworkBall.Instance.ServerServe(towardSide: 1 - Side.Value);   // the SERVER launches the ball
        GameState.Announce($"Player {OwnerId} ({SideName(Side.Value)}) served");
    }

    [TargetRpc]
    private void TargetRefused(NetworkConnection target, string why)
    {
        ConnectionHud.Flash(why);
    }

    [ObserversRpc]                                      // server -> every client that observes this object
    public void ObserversScored(int left, int right)
    {
        Debug.Log($"[Client] Goal! Player {OwnerId} ({SideName(Side.Value)}) scored. {left} : {right}");
        ConnectionHud.Flash($"{SideName(Side.Value)} scored!  {left} : {right}");
    }

    [TargetRpc]                                         // server -> exactly one client, the connection passed as the first argument
    public void TargetYouServe(NetworkConnection target)
    {
        Debug.Log("[Client] YOU serve. Press Space.");
        ConnectionHud.Flash("YOU SERVE — press Space");
    }

    // ---- presentation ----

    private void Place()
    {
        int side = Side.Value;
        Renderer r = GetComponent<Renderer>();
        if (side == Spectator)
        {
            if (r != null) r.enabled = false;           // a spectator's paddle exists but is not drawn
            transform.position = new Vector3(0, -5f, 0);
            return;
        }
        if (r != null) r.enabled = true;
        transform.position = new Vector3(side == Left ? -Court.PaddleX : Court.PaddleX, 0.5f, transform.position.z);
        Court.Paint(gameObject, side == Left ? leftColor : rightColor);
    }
}
