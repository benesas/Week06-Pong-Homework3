// Pong — the networked paddle, Week 2 version (Steps 4, 6 and 8 of the build).
// Step 4: a NetworkBehaviour; only the OWNER reads the keyboard; the SERVER assigns a side.
// Step 6: a NetworkTransform COMPONENT on the prefab (no code): the owner's position replicates.
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
        if (Local == this) Local = null;
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

    // ---- gameplay (owner only) ----

    private void Update()
    {
        // THE most important line of the block. Without it, every client moves every paddle.
        if (!IsOwner) return;
        if (Side.Value == Spectator) return;            // spectators watch

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float dir = 0f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)   dir += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir -= 1f;

        // Still a plain local move. The NetworkTransform component on this prefab notices the
        // change and sends it to the server, which forwards it to every other client (Step 6).
        transform.position += Vector3.forward * dir * speed * Time.deltaTime;
        Court.ClampPaddle(transform);

        if (kb.spaceKey.wasPressedThisFrame)
            ServerRequestServe();                       // ask; do not decide
    }

    // ---- the serve and the goal: three RPCs, three directions ----

    [ServerRpc]                                         // owner -> server. Only the owner may call it (default RequireOwnership = true).
    private void ServerRequestServe()
    {
        GameState gs = GameState.Instance;
        if (gs == null || NetworkBall.Instance == null) return;
        if (gs.ServingSide.Value != Side.Value) return;         // not your serve: the request is refused, nothing happens anywhere
        if (NetworkBall.Instance.InPlay.Value) return;          // stale: the ball is already moving

        NetworkBall.Instance.ServerServe(towardSide: 1 - Side.Value);   // the SERVER launches the ball
        GameState.Announce($"Player {OwnerId} ({SideName(Side.Value)}) served");
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
