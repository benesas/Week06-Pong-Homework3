# Week 5 — Pong on a host

DGD0005 Multiplayer Programming · Week 5 in-class assignment.
Unity **6000.3.2f1**, FishNet **4.7.3R**, Multiplayer Play Mode **2.0.2**.

Everything below was done on the Homework 1 Pong (single ball, score, serve, feed).
The only thing that changed this week is **who runs the server**: one process now runs
both halves and a guest joins it like any other server.

---

## What changed in the code

| File | Change |
| --- | --- |
| `Assets/Scripts/Network/ConnectionHud.cs` | **Start Host** (only when both sides are `Stopped`) and **Stop Host** (only when both are `Started`). Start Host calls `ServerManager.StartConnection()` **then** `ClientManager.StartConnection("127.0.0.1", port)`. Stop Host stops the client **then** the server. HUD also prints `Paddles known here: <All.Count>`. Added `-host` command-line switch (same as one click) next to the existing `-connect host:port`. |
| `Assets/Scripts/Network/ConnectionLogger.cs` | On client `Started`, if `ServerManager.Started` is also true: `[Host] This process is the server AND a client. RTT is 0 ms.` The ClientId line waits one frame because FishNet assigns `ClientId` just after the `Started` callback (otherwise it logs `-1`). |
| `Assets/Scripts/Network/PaddleController.cs` | Both `All.Add` calls were already guarded with `if (!All.Contains(this))`; the guard and the reason are now documented, and `All.Count` is logged after every spawn/despawn (and shown in the HUD). |

Scene / project settings verified:

* `ServerManager ▸ Start On Headless` = **off** (`_startOnHeadless: 0`).
* `NetworkManager ▸ Run In Background` = **on** (`_runInBackground: 1`).
* `TimeManager ▸ Tick Rate` = **30** (back from the Step 5 experiment).
* `PlayerSpawner ▸ Player Prefab` = `Assets/Prefabs/Paddle.prefab`; no paddle in the scene.
* `Assets/Scenes/Pong.unity` is the only enabled scene in Build Settings.
* Packages: FishNet 4.7.3R + `com.unity.multiplayer.playmode` 2.0.2.

---

## Step 3 — both callbacks, one paddle

Observed Console on the host, one click on **Start Host**:

```text
[Server] Started
[Server] Client 0 connected. Now 1 client(s).
[Client] Started
[Client] Connected. The server calls me client 0.
[Host] This process is the server AND a client. RTT is 0 ms.
[Server] Spawned paddle #18157 for client 0: LEFT.
[Server] Paddles known here: 1.
[Client] I am client 0, playing LEFT. My paddle is #18157. Only I read the keyboard for it.
[Feed/server] Player 0 joined as LEFT
[Feed] Player 0 joined as LEFT
```

* **Which callback ran first:** `OnStartServer` — its `[Server] Spawned paddle …` line is
  printed before `OnStartClient`'s `[Client] I am client 0 …` line.
* **Which half of the host ran it:** the **server half**. `OnStartServer` only runs because
  this process is the server; `OnStartClient` then runs on the **client half** of the same
  process, on the same `NetworkObject`. One object, two halves, two callbacks.
* **The guard:** both `All.Add(this)` calls are inside `if (!All.Contains(this))`. Without
  it, the host would add the same paddle twice and `All.Count` would be **2** for one paddle.
  With it, the HUD/log says **1**.
* The feed appears twice on the host (`[Feed/server]` and `[Feed]`): once on the server half
  and once on the client half. That is the same one-object-two-halves fact, not a second goal.

---

## Step 5 — the host wins

Verified on the loopback host + guest:

```text
[Server] Client 1 connected. Now 2 client(s).
[Server] Spawned paddle #29284 for client 1: RIGHT.
[Server] Paddles known here: 2.
[Client] Another paddle appeared: client 1, RIGHT, object #29284. NetworkTransform moves it for me.
```

The host is `client 0`, LEFT; the guest is `client 1`, RIGHT; a third connection becomes
`SPECTATOR` (paddle exists, hidden, no input). The guest's paddle on the host is a copy moved
by the owner's `NetworkTransform`, one tick old; the host's own paddle is the server's paddle.

* **Guest RTT in this run:** 0 ms (loopback — the automated host and guest were on the same
  Mac, so RTT tells us nothing). A real LAN RTT can only be measured with a second machine
  (see the connection card below).
* **Why the ball looks different on the guest:** the server bounces the ball against its copy
  of the guest's paddle. When the guest sees the ball pass its paddle, the server had not yet
  received the newest paddle position; the next snapshot snaps the guest's paddle back and the
  ball bounces. On the host there is no copy and no delay: the server's paddle **is** the
  host's paddle.
* **Serve:** the host's Space reaches `ServerRequestServe` and the server serves in the same
  process (no RTT). The guest's Space travels to the server (~RTT/2), is validated, then the
  ball state travels back (~RTT/2 + interpolation): the guest's serve starts about one RTT
  later.
* **Tick 10 experiment:** at 10 ticks/s the ball is replicated 3× less often, so on the guest
  it visibly steps/jumps and the pass-through-then-snap effect is much easier to see; on the
  host it is still smooth because the server simulates at frame rate. Tick rate is back at 30.

---

## Step 6 — the host leaves

Partially verified automatically (the host process was terminated mid-rally):

* The guest keeps the last frame, then logs `[Client] Stopping` and, after the remote timeout
  (60 s + 2 s check in this project), `[Client] Stopped`.
* All server-spawned objects (both paddles, the ball) and the scene `GameState` are despawned /
  destroyed on the guest: nothing exists unless the server says so. The HUD loses score and feed.
* Starting the host and the client again gives new object ids and a fresh `0 : 0`.

The other direction (guest clicks Stop Client): the host logs `Client 1 disconnected`, removes
the RIGHT paddle (`Paddles known here: 1`), resets the ball and keeps playing. Verified by
killing the guest; the server waits for its timeout on an abrupt process kill, while a real
**Stop Client** disconnects immediately.

---

## Step 7 — connection card

> **TODO before handing in:** this card needs a second PC and a partner. It cannot be filled
> honestly from a single machine. Fill the blanks during the partner session.

| Field | Value |
| --- | --- |
| Partner's name | _fill in_ |
| Host's LAN address (`ipconfig getifaddr en0`) | _fill in_ |
| My ClientId | _fill in_ (host is `0`; first guest is `1`) |
| My RTT | _fill in_ (a few ms on cable; `0` on the host tells you nothing) |
| Connected first try? | _yes / no_ |
| If no, which fix line worked | _fill in (Section 4: IP, firewall, wired LAN/hotspot, headless)_ |

What was verified locally: guest joins `127.0.0.1:7770` on the first try, host sees
`Client 1 connected`, guest is RIGHT, paddle replicates to both machines, third client is a
spectator.

---

## Step 8 — the same scripts, three ways

1. **Start Server only:** `ServerBootstrap` / `ConnectionLogger` server half / `GameState` /
   `NetworkBall` / `BallSpawner` / `PlayerSpawner` run. No client half, no local paddle, no
   keyboard, HUD shows only server lines. This is the Week 4 Edgegap process.
2. **Start Host:** the server half **and** the client half of the same process run. Scripts
   that run twice (once per half): `ConnectionLogger` (server + client callbacks),
   `PaddleController` (`OnStartServer` + `OnStartClient` on the same object — guarded),
   `GameState` feed logging (`[Feed/server]` + `[Feed]`). `NetworkBall.Update` still runs
   only on the server half (`IsServerInitialized`).
3. **Start Client:** the guest knows the host's address and port (what it typed), its own
   `ClientId`, the replicated `GameState` (score, serve, feed) and every object the server
   spawned for it. It does **not** know the host's process, its RTT to itself, or any
   server-only state (`_velocity`, `ByOwner`).

---

## Self-review

**1. Start Host: which two calls? Why is the host's RTT 0?**
`NetworkManager.ServerManager.StartConnection()` then
`NetworkManager.ClientManager.StartConnection("127.0.0.1", port)`. Both halves live in one
process and the client connects over loopback/in-memory; the packet never leaves the machine
and the server handles it in the same tick, so the measured round trip rounds to 0 ms.

**2. The guest's paddle exists on the host, the guest and a spectator.**
For each: `IsOwner`, `IsServerInitialized`, callbacks, keyboard.

| Where | `IsOwner` | `IsServerInitialized` | Callbacks that ran | Reads the keyboard |
| --- | --- | --- | --- | --- |
| Guest (owner) | `true` | `false` | `OnStartClient` (owner branch) | **yes**, this copy |
| Host (server half) | `false` | `true` | `OnStartServer`, plus `OnStartClient` because the host is also a client | no |
| Spectator | `false` | `false` | `OnStartClient` (other-paddle branch) | no |

The server copy is moved by `NetworkTransform` from the owner's updates; the spectator's copy
is moved by replication. Only the owner passes `if (!IsOwner) return;`.

**3. Both serves, traced.**
Host: `Update` (IsOwner) → `ServerRequestServe` `[ServerRpc]` (same process) → server validates
`ServingSide` and `InPlay` → `NetworkBall.ServerServe` sets `_velocity` and `InPlay.Value = true`
→ the ball moves on the server copy immediately. No wait.
Guest: `Update` → `ServerRequestServe` travels to the server (≈ RTT/2) → validation →
`ServerServe` → `InPlay` and the ball's `NetworkTransform` travel back (≈ RTT/2 + interpolation)
→ the ball moves on the guest. **The guest waits about one RTT; the host waits nothing.**

**4. Step 6, both directions.**
The guest's own paddle vanished because the server owns existence: it spawned the paddle and
replicates it; when the server is gone the client times out, despawns every server-spawned
object and destroys the scene objects (`GameState`, ball), so score and feed go too. The host
survives a guest leaving because the guest was only a connection and an owned object; the
server despawns that one paddle, resets the ball and keeps `GameState` and the score.

**5. State or event?**
score → **state** (`SyncVar<int>`); "RIGHT scored!" → **event** (`ObserversRpc`); whose serve →
**state** (`SyncVar<int> ServingSide`); "YOU SERVE" → **event**, targeted (`TargetRpc`); the
ball's position → **state** (`NetworkTransform` snapshots, interpolated); the feed → **state**
(`SyncList<string>`; a late joiner receives the operations and sees the same list).

**6. A guest writes `GameState.Instance.LeftScore.Value = 9`.**
Nothing authoritative happens: a client is not the server, `SyncVar` writes from a client are
not replicated (FishNet ignores/rejects them) and the next server value wins. It may flicker
locally for a frame; every other machine still sees the server's score. On the host the same
line works because the host's client half shares the object with the server half, and
`IsServerInitialized` is true, so the write is the server's write.

**7. The feed logs twice on the host.**
Once from the server half (`[Feed/server]`) and once from the client half (`[Feed]`). The line
that stops a goal sound from playing twice is the presentation guard in `OnFeedChanged`:
`if (!asServer) { /* sound / flash */ }` — play only on the client half (equivalently
`if (IsServerInitialized) return;` for presentation). The score log does not double because
`LeftScore.OnChange` / `RightScore.OnChange` already log only when `!asServer`.

---

## How to run

1. Open the project with Unity **6000.3.2f1**.
2. Open `Assets/Scenes/Pong.unity`.
3. Play → **Start Host** → one process is server + client (LEFT paddle, RTT 0).
4. Guest: another Play Mode instance (Multiplayer Play Mode, remove the *Server* tag) or a
   build launched with `-connect <host-ip>`; or use the HUD and type the address.
5. Command line helpers: `-host` (server + client in one process) and
   `-connect host[:port]` (guest).

A macOS Mono test build was used during development to verify the host and a loopback guest; it is not part of the package.
