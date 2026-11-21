#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class Week06NetworkSmoke : MonoBehaviour
{
    private string _role;
    private double _start;
    private Keyboard _keyboard;
    private int _failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfRequested()
    {
        string[] args = Environment.GetCommandLineArgs();
        int role = Array.IndexOf(args, "-smoke-role");
        int start = Array.IndexOf(args, "-smoke-start");
        if (role < 0 || start < 0 || role + 1 >= args.Length || start + 1 >= args.Length) return;
        var test = new GameObject("Week06 network smoke test").AddComponent<Week06NetworkSmoke>();
        test._role = args[role + 1];
        test._start = double.Parse(args[start + 1], System.Globalization.CultureInfo.InvariantCulture);
        test.StartCoroutine(test.Run());
    }

    private double Elapsed => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 - _start;
    private IEnumerator At(double seconds) { while (Elapsed < seconds) yield return null; }
    private void Check(bool condition, string message)
    {
        if (!condition) _failures++;
        Debug.Log($"[Smoke/{_role}] {(condition ? "PASS" : "FAIL")} {message}");
    }
    private IEnumerator Press(Key key)
    {
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
        yield return new WaitForSecondsRealtime(0.08f);
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
        yield return null;
    }
    private string Flash => (string)typeof(ConnectionHud).GetField("_flash", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

    private IEnumerator Run()
    {
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        _keyboard = InputSystem.AddDevice<Keyboard>();
        yield return At(8);
        bool ready = PaddleController.Local != null && PaddleController.All.Count == 2;
        Check(ready, "Two players spawned");
        if (!ready) { Application.Quit(1); yield break; }
        Check(PaddleController.Local.Side.Value == (_role == "host" ? 0 : 1), "Expected player side");
        yield return At(10);
        if (_role == "guest") yield return Press(Key.G);
        yield return At(11);
        Check(GameState.Instance.LeftScore.Value == 0, "Guest G cannot edit authoritative score");
        yield return At(12);
        if (_role == "guest") yield return Press(Key.C);
        yield return At(14);
        float before = PaddleController.Local.transform.position.z;
        if (_role == "guest") yield return Press(Key.X);
        yield return At(15);
        if (_role == "guest")
        {
            float delta = PaddleController.Local.transform.position.z - before;
            Check(delta > 0.1f && delta < 0.4f, "X is clamped to one normal tick of movement");
        }
        yield return At(16);
        if (_role == "guest")
        {
            yield return Press(Key.Space);
            yield return Press(Key.Space);
        }
        yield return At(17);
        Check(_role == "guest" ? Flash == "TOO FAST" : Flash != "TOO FAST", "Serve refusal reaches only requester");
        yield return At(19);
        if (_role == "host") yield return Press(Key.G);
        yield return At(20);
        Check(GameState.Instance.LeftScore.Value == 9, "Host G score reaches both processes");
        string[] args = Environment.GetCommandLineArgs();
        int evidence = Array.IndexOf(args, "-smoke-evidence");
        if (_role == "guest" && evidence >= 0 && evidence + 1 < args.Length)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(args[evidence + 1]);
        }
        yield return At(21);
        if (_role == "host") NetworkBall.Instance.transform.position = new Vector3(0, Court.BallY, 2);
        yield return At(22);
        if (_role == "host") yield return Press(Key.T);
        yield return At(23);
        Check(Mathf.Abs(PaddleController.ForSide(0).transform.position.z - 2) < 0.1f, "Host T position reaches both processes");
        yield return At(24);
        if (_role == "guest") yield return Press(Key.T);
        yield return At(25);
        Check(PaddleController.ForSide(1).transform.position.z < 1, "Guest T cannot persist against server state");
        yield return At(27);
        if (_role == "host")
        {
            var speed = typeof(PaddleController).GetField("speed", BindingFlags.Instance | BindingFlags.NonPublic);
            var paddle = PaddleController.Local;
            paddle.transform.position = new Vector3(-Court.PaddleX, 0.5f, 0);
            speed.SetValue(paddle, 40f);
            yield return Press(Key.W);
            yield return new WaitForSecondsRealtime(0.2f);
            Check(paddle.transform.position.z > 1, "Host server speed can be changed to 40");
            speed.SetValue(paddle, 8f);
        }
        yield return At(29);
        Debug.Log($"[Smoke/{_role}] COMPLETE failures={_failures}");
        InputSystem.RemoveDevice(_keyboard);
        Application.Quit(_failures == 0 ? 0 : 1);
    }
}
#endif
