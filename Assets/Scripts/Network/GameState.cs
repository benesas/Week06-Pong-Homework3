// Pong — one scene object that holds shared state that belongs to nobody in particular
// (Steps 7 and 9 of the Week 2 build): the two scores, whose serve it is, and an event feed.
//
// Scores are SyncVars: the server writes them once per goal, every client mirrors them, and a
// client that joins in the middle of a match sees 3 : 2 from its first frame, with no catch-up code.
// The feed is a SyncList: a SyncVar for a whole list. FishNet sends only the OPERATION
// ("add 'LEFT scored' at index 4"), not the list. Same rule as SyncVar: only the server writes.
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class GameState : NetworkBehaviour
{
    public static GameState Instance { get; private set; }

    private const int FeedLength = 6;

    // ---- replicated state: server writes, everyone reads ----
    public readonly SyncVar<int> LeftScore   = new SyncVar<int>();
    public readonly SyncVar<int> RightScore  = new SyncVar<int>();
    public readonly SyncVar<int> ServingSide = new SyncVar<int>(PaddleController.Left);   // who may press Space; LEFT serves first

    public readonly SyncList<string> Feed = new SyncList<string>();

    private void Awake()
    {
        Instance = this;
        Feed.OnChange += OnFeedChanged;
        LeftScore.OnChange  += (prev, next, asServer) => { if (!asServer) Debug.Log($"[Client] LEFT score {prev} -> {next}"); };
        RightScore.OnChange += (prev, next, asServer) => { if (!asServer) Debug.Log($"[Client] RIGHT score {prev} -> {next}"); };
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// Server-only. Called by NetworkBall when the ball crossed a goal line.
    /// The shape of every server rule: validate -> change SyncVars -> announce with RPCs.
    public void ServerGoal(int scorerSide)
    {
        if (!IsServerInitialized) return;

        if (scorerSide == PaddleController.Left) LeftScore.Value += 1;      // STATE: one SyncVar write, every scoreboard updates
        else                                     RightScore.Value += 1;
        ServingSide.Value = 1 - scorerSide;                                  // the player who conceded serves next

        PaddleController scorer = PaddleController.ForSide(scorerSide);
        if (scorer != null) scorer.ObserversScored(LeftScore.Value, RightScore.Value);     // EVENT: everyone flashes it

        Announce($"{PaddleController.SideName(scorerSide)} scored: {LeftScore.Value} : {RightScore.Value}");

        ServerPlayersChanged();                                              // the conceding side may be empty: hand the serve to someone who exists
        PaddleController server = PaddleController.ForSide(ServingSide.Value);
        if (server != null) server.TargetYouServe(server.Owner);            // PRIVATE: one client hears "you serve"
    }

    /// Server-only. Called when a paddle spawns or despawns and after every goal.
    /// A dedicated server lives for hours and players come and go, so the match must never get stuck:
    /// the serve always belongs to a side that has a player, and an empty court starts a fresh match.
    public void ServerPlayersChanged()
    {
        if (!IsServerInitialized) return;
        bool left  = PaddleController.ForSide(PaddleController.Left)  != null;
        bool right = PaddleController.ForSide(PaddleController.Right) != null;

        if (!left && !right)
        {
            // Court is empty. Reset so the next players start at 0 : 0 instead of inheriting the last match.
            LeftScore.Value = 0;
            RightScore.Value = 0;
            ServingSide.Value = PaddleController.Left;
            Feed.Clear();
            NetworkBall.Instance?.ServerReset();
            Announce("New match. Waiting for players");
            return;
        }

        // A player left in the middle of a rally: hold the ball instead of scoring against nobody.
        if (!(left && right) && NetworkBall.Instance != null && NetworkBall.Instance.InPlay.Value)
            NetworkBall.Instance.ServerReset();

        // The serving side must have a player, otherwise nobody can press Space and the game is dead.
        if (PaddleController.ForSide(ServingSide.Value) == null)
            ServingSide.Value = left ? PaddleController.Left : PaddleController.Right;
    }

    /// Server-only. Anyone on the server may call this; it is ignored on clients.
    public static void Announce(string message)
    {
        if (Instance == null || !Instance.IsServerInitialized) return;
        Instance.Feed.Add(message);
        while (Instance.Feed.Count > FeedLength) Instance.Feed.RemoveAt(0);
    }

    private void OnFeedChanged(SyncListOperation op, int index, string oldItem, string newItem, bool asServer)
    {
        // Every machine, every operation. We only log additions; the HUD reads the list directly.
        if (op == SyncListOperation.Add)
            Debug.Log($"[Feed{(asServer ? "/server" : "")}] {newItem}");
    }
}
