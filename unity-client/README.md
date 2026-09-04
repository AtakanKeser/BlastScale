# BlastScale Unity client

A small Unity (C#) client for the BlastScale backend. It is deliberately thin: the server owns
every decision that matters (seed, score, stars, rewards, lives) and the client is a renderer that
records what the player did and asks the server to judge it.

## Opening the project

* Unity **6000.3.10f1** (Unity 6). Open the `unity-client` folder with the Unity Hub / Editor;
  `ProjectSettings/ProjectVersion.txt` pins the version. `Library/`, `Temp/`, `Logs/`, `obj/` and
  `UserSettings/` are build products and are ignored by the repository's `.gitignore`.
* Packages (see `Packages/manifest.json`): `com.unity.ugui` (runtime UI), `com.unity.nuget.newtonsoft-json`
  (JSON), `com.unity.test-framework` (tests) plus the `ui` and `unitywebrequest` modules. The new Input
  System package is intentionally **not** used; the scene uses the legacy `StandaloneInputModule`.
* The only scene is `Assets/Scenes/Main.unity` (camera, `EventSystem`, `GameBootstrap`). It is generated
  by `Assets/Scripts/Editor/SceneBuilder.cs` — menu *BlastScale > Build Main Scene*, or headless:

  ```bash
  "/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -quit \
    -projectPath unity-client -executeMethod BlastScale.EditorTools.SceneBuilder.BuildMainScene -logFile /tmp/unity-compile.log
  ```

* Press Play in `Main.unity`. The whole UI (canvas, screens, board) is built from code at runtime with
  UGUI — no prefabs, no sprites, no TextMeshPro — using the built-in `LegacyRuntime.ttf` font.

## Pointing the client at a server

The base URL defaults to `http://localhost:8080` (`Assets/Scripts/Net/ClientConfig.cs`). The login
screen has a *Server URL* field; whatever is entered there is stored in `PlayerPrefs`
(`blastscale.baseUrl`) and used for every request from then on. Clear the field (or enter the default)
to go back to `localhost`. All endpoints live under `/api/v1` (`Assets/Scripts/Net/ApiRoutes.cs`).

Start the backend with `docker compose up --build` in the repository root (see the root README).

## Screen flow

```
Login ──(guest / login / register)──► Home ──► Gameplay ──► Result ──► (Next level) Gameplay
                                       │  ▲                                │
                                       │  └────────────── Home ◄───────────┘
                                       ├──► Shop         (boosters, life refill)
                                       ├──► Leaderboard  (weekly top 50 + own rank)
                                       └──► Events       (live events, rocket race standings)
```

* **Login** — `POST /auth/guest` with the device id (or username/password login/register). The bearer
  token is kept in memory (`GameState`) and sent as `Authorization: Bearer <token>`.
* **Home** — `GET /players/me`, `GET /config`, `GET /economy/daily-reward`, `GET /levels/{n}`. Shows level,
  coins, stars, lives with a local regeneration countdown (from `nextLifeInSeconds`, refreshed from the
  server when it reaches zero), the experiment variants the player is bucketed into, and the doors to
  the other screens. Daily reward claims are `POST /economy/daily-reward` with an `Idempotency-Key`.
* **Gameplay** — `POST /levels/{n}/start` consumes a life and returns a seed + board rules. The client
  builds the board locally (`BoardState(config, seed)`) and records every move. Boosters: Hammer
  (remove one block), Shuffle (regenerate the board), +5 Moves (once per attempt). *Finish* unlocks
  when the target score is reached; running out of moves auto-submits a win or, after offering the
  +5 Moves booster, a loss.
* **Result** — shows what the **server** answered to `POST /levels/{n}/complete` (score, stars, reward
  strategy and multiplier, event points, wallet) or `POST /levels/{n}/fail`.
* **Shop** — prices from remote config (`boosterPrices`, `lifeRefillPrice`); `POST /economy/shop/boosters`
  and `POST /economy/shop/lives`, each with an `Idempotency-Key`.
* **Leaderboard** / **Events** — `GET /leaderboards/weekly?limit=50` and `GET /events`.

Errors use the backend's uniform body `{code, message, details, timestamp, path}`: `message` is shown
to the player, `code` drives behaviour (`NO_LIVES_LEFT` opens a "next life in …" dialog with a shop
shortcut, `IDEMPOTENT_REQUEST_IN_PROGRESS` is retried after a second, a 401 sends the player back to
the login screen). A dropped connection is retried once with the **same** `Idempotency-Key`.

## How the client relates to the server

* The gameplay engine in `Assets/Scripts/Engine/` is a line-by-line port of the server's
  `level/engine` package (`SeededRandom`, `BoardConfig`, `Move`, `MoveType`, `BoardState`, `BoardEngine`,
  `SimulationResult`). It is pure C# (`noEngineReferences: true` in its asmdef) so it runs identically on
  both sides: same 32-bit LCG, same row-major fill, same 4-connected pop, gravity, refill and
  "ensure playable" regeneration.
* When a level starts, the server chooses the seed. The client only renders the board that seed
  produces and records the player's moves (`TAP`, `HAMMER`, `SHUFFLE` with row/col) plus whether the
  +5 Moves booster was used.
* On completion the client sends the move list. **The server replays it on its own engine copy and
  computes the score, stars and reward itself**; the client's `score`/`movesUsed` are only
  cross-checked. Anything the client could fake (score, moves, boosters) is therefore worthless: a
  forged move list simply fails the replay (`INVALID_MOVE_SEQUENCE`, `SCORE_MISMATCH`,
  `OBJECTIVE_NOT_REACHED`), and completions faster than 150 ms per tap are rejected as
  `SUSPICIOUS_DURATION`.
* Wallet snapshots returned by the economy and progression endpoints replace the local copy; the
  client never computes balances on its own.

## Code layout

```
Assets/Scripts/Engine/      pure C# engine port (asmdef BlastScale.Engine, no UnityEngine)
Assets/Scripts/Net/         ApiClient (UnityWebRequest + Newtonsoft), ApiRoutes, ClientConfig, Dto/*
Assets/Scripts/Core/        GameState, LevelSession, GameFlow, GameBootstrap (the scene's only component)
Assets/Scripts/UI/          UiFactory (runtime UGUI helpers), ScreenManager, Toast, ModalDialog, Screens/*
Assets/Scripts/Editor/      SceneBuilder (generates Main.unity)
Assets/Tests/Editor/        EngineVectorTests + engine-vectors.json (parity with the Java engine)
Assets/Tests/PlayMode/      SceneSmokeTests (boots Main.unity and checks the login screen appears)
```

## Tests

`Assets/Tests/Editor/EngineVectorTests.cs` replays every case of `docs/engine/engine-vectors.json`
(copied next to the test) and asserts the initial board, the replay summary (`valid`, `finalScore`,
`finalMovesUsed`, `hammersUsed`, `shufflesUsed`, `objectiveReached`, `stars`) and the final board
match the Java engine, plus the RNG reference sequence. Run them from *Window > General > Test Runner*
(EditMode) or headless:

```bash
"/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics \
  -runTests -testPlatform EditMode -projectPath unity-client -testResults /tmp/unity-tests.xml -logFile /tmp/unity-tests.log
```

The PlayMode smoke test runs the same way with `-testPlatform PlayMode`.
