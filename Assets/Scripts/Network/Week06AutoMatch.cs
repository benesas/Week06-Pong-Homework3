#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FishNet;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[DefaultExecutionOrder(-100)]
public class Week06AutoMatch : MonoBehaviour
{
    [Serializable] private class MatchResult
    {
        public string role, player, connection = "127.0.0.1 / local UnityTransport";
        public int left, right, goalsObserved;
        public double durationSeconds;
        public long guestRttMs;
        public bool completed, scoreWrittenDirectly = false;
        public string error = "";
        public List<string> events = new List<string>();
    }
    private Keyboard _keyboard;
    private MatchResult _result;
    private string _directory, _role;
    private float _started, _rallyStarted, _nextServe;
    private bool _wasInPlay, _finishing;
    private int _lastTotal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartIfRequested()
    {
        string[] args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, "-auto-role");
        int directory = Array.IndexOf(args, "-auto-report");
        if (flag < 0 || directory < 0 || flag + 1 >= args.Length || directory + 1 >= args.Length) return;
        var bot = new GameObject("Week06 automatic match player").AddComponent<Week06AutoMatch>();
        bot._role = args[flag + 1];
        bot._directory = args[directory + 1];
        bot._result = new MatchResult { role = bot._role };
        if (Array.IndexOf(args, "-relay-host") >= 0 || Array.IndexOf(args, "-relay-join") >= 0)
            bot._result.connection = "Unity Relay DTLS / two processes on same PC";
        Directory.CreateDirectory(bot._directory);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        bot._keyboard = InputSystem.AddDevice<Keyboard>();
        bot._started = Time.realtimeSinceStartup;
    }

    private void Update()
    {
        if (_result == null || _finishing) return;
        float elapsed = Time.realtimeSinceStartup - _started;
        if (elapsed > 180) { StartCoroutine(Finish("Match timeout")); return; }
        var me = PaddleController.Local;
        var gs = GameState.Instance;
        var ball = NetworkBall.Instance;
        if (me == null || gs == null || ball == null || PaddleController.All.Count != 2) return;
        _result.player = PaddleController.SideName(me.Side.Value);
        int total = gs.LeftScore.Value + gs.RightScore.Value;
        if (total != _lastTotal)
        {
            string line = $"{elapsed:F2}s score {gs.LeftScore.Value}:{gs.RightScore.Value}";
            _result.events.Add(line);
            Debug.Log($"[AutoMatch/{_role}] {line}");
            _lastTotal = total;
            _result.goalsObserved = total;
        }
        if (gs.LeftScore.Value >= 3 || gs.RightScore.Value >= 3)
        {
            _result.left = gs.LeftScore.Value;
            _result.right = gs.RightScore.Value;
            _result.completed = true;
            StartCoroutine(Finish(""));
            return;
        }

        bool inPlay = ball.InPlay.Value;
        if (inPlay && !_wasInPlay) _rallyStarted = Time.realtimeSinceStartup;
        _wasInPlay = inPlay;
        float target = ball.transform.position.z;

        int plannedWinner = total % 2 == 0 ? PaddleController.Left : PaddleController.Right;
        if (inPlay && Time.realtimeSinceStartup - _rallyStarted > 5 && me.Side.Value != plannedWinner)
            target = ball.transform.position.z >= 0 ? -3.9f : 3.9f;
        float difference = target - me.transform.position.z;
        Key movement = difference > 0.18f ? Key.W : difference < -0.18f ? Key.S : Key.None;
        bool serve = !inPlay && gs.ServingSide.Value == me.Side.Value && Time.realtimeSinceStartup >= _nextServe;
        if (serve) _nextServe = Time.realtimeSinceStartup + 1.1f;
        if (serve && movement != Key.None) InputSystem.QueueStateEvent(_keyboard, new KeyboardState(movement, Key.Space));
        else if (serve) InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
        else if (movement != Key.None) InputSystem.QueueStateEvent(_keyboard, new KeyboardState(movement));
        else InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
    }

    private IEnumerator Finish(string error)
    {
        _finishing = true;
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
        _result.error = error;
        _result.durationSeconds = Time.realtimeSinceStartup - _started;
        _result.guestRttMs = (long)InstanceFinder.TimeManager.RoundTripTime;
        File.WriteAllText(Path.Combine(_directory, _role + "-result.json"), JsonUtility.ToJson(_result, true));
        Debug.Log($"[AutoMatch/{_role}] COMPLETE success={_result.completed} score={_result.left}:{_result.right} error={error}");

        yield return new WaitForSecondsRealtime(0.5f);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(_directory, _role + "-final.png"));
        yield return new WaitForSecondsRealtime(_role == "host" ? 3f : 2f);
        var manager = InstanceFinder.NetworkManager;
        if (manager != null)
        {
            manager.ClientManager.StopConnection();
            yield return new WaitForSecondsRealtime(0.25f);
            if (manager.ServerManager.Started) manager.ServerManager.StopConnection(true);
            yield return new WaitForSecondsRealtime(0.25f);
        }
        InputSystem.RemoveDevice(_keyboard);
        yield return null;
        Application.Quit(_result.completed ? 0 : 1);
    }
}
#endif
