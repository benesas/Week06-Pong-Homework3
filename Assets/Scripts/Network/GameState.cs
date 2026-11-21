using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class GameState : NetworkBehaviour
{
    public static GameState Instance { get; private set; }

    private const int FeedLength = 6;

    public readonly SyncVar<int> LeftScore   = new SyncVar<int>();
    public readonly SyncVar<int> RightScore  = new SyncVar<int>();
    public readonly SyncVar<int> ServingSide = new SyncVar<int>(PaddleController.Left);

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

    public void ServerGoal(int scorerSide)
    {
        if (!IsServerInitialized) return;

        if (scorerSide == PaddleController.Left) LeftScore.Value += 1;
        else                                     RightScore.Value += 1;
        ServingSide.Value = 1 - scorerSide;

        PaddleController scorer = PaddleController.ForSide(scorerSide);
        if (scorer != null) scorer.ObserversScored(LeftScore.Value, RightScore.Value);

        Announce($"{PaddleController.SideName(scorerSide)} scored: {LeftScore.Value} : {RightScore.Value}");

        ServerPlayersChanged();
        PaddleController server = PaddleController.ForSide(ServingSide.Value);
        if (server != null) server.TargetYouServe(server.Owner);
    }

    public void ServerPlayersChanged()
    {
        if (!IsServerInitialized) return;
        bool left  = PaddleController.ForSide(PaddleController.Left)  != null;
        bool right = PaddleController.ForSide(PaddleController.Right) != null;

        if (!left && !right)
        {

            LeftScore.Value = 0;
            RightScore.Value = 0;
            ServingSide.Value = PaddleController.Left;
            Feed.Clear();
            NetworkBall.Instance?.ServerReset();
            Announce("New match. Waiting for players");
            return;
        }

        if (!(left && right) && NetworkBall.Instance != null && NetworkBall.Instance.InPlay.Value)
            NetworkBall.Instance.ServerReset();

        if (PaddleController.ForSide(ServingSide.Value) == null)
            ServingSide.Value = left ? PaddleController.Left : PaddleController.Right;
    }

    public static void Announce(string message)
    {
        if (Instance == null || !Instance.IsServerInitialized) return;
        Instance.Feed.Add(message);
        while (Instance.Feed.Count > FeedLength) Instance.Feed.RemoveAt(0);
    }

    private void OnFeedChanged(SyncListOperation op, int index, string oldItem, string newItem, bool asServer)
    {

        if (op == SyncListOperation.Add)
            Debug.Log($"[Feed{(asServer ? "/server" : "")}] {newItem}");
    }
}
