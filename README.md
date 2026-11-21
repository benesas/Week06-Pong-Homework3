# Week06 Pong

Open with **Unity 6000.3.2f1** and load `Assets/Scenes/Pong.unity`.

- [Assignment guide and answers](Week06/README.md)
- [Verification results](Week06/TEST_RESULTS.md)
- [Automated matches](Week06/AUTOMATED_MATCHES.md)
- [Unity Cloud and Relay evidence](Week06/RELAY_TEST.md)

This project continues the scene, prefabs and FishNet code supplied in Week05_Pong_Host.unitypackage.

Implemented: server-authoritative movement, a 30 ms input limit, C/X/T/G cheat demonstrations, a one-second per-player serve limit, private refusal messages, UnityTransport and Relay join codes.

Two local bot matches completed with host/guest roles swapped. Both finished 3-2. Select **Week06 > Build and play automatically (two players)** to repeat. Missing Relay dependencies can be installed through the included Editor helper.

Cloud project **pong** is linked and Relay is enabled. Two processes also connected through a real Relay join code and played to 3 points. All processes ran on one computer; the real partner test on two different networks remains pending. Repository: https://github.com/benesas/Week06-Pong-Homework3. The local development commits are included; their timestamps do not establish a timely remote submission.

Feed and technical debug rows are hidden by default. Score, RTT, join code and serve/refusal notifications remain visible.

The smoke and bot drivers run only in the Editor or Development builds, and only when explicitly requested with `-smoke-role` or `-auto-role`. They do not run during normal play.

## Downloadable deliverables

- [Unity asset package](Deliverables/Week06_Pong_Completed.unitypackage)
- [Complete source ZIP](Deliverables/Week06_Pong_Project.zip)

The repository root is a Unity project. Add this folder in Unity Hub. The ZIP is a packaged snapshot; repository documentation records the latest upload status.
