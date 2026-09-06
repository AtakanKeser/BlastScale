# BlastScale Unity client

A Unity (C#) client for the BlastScale backend. It is deliberately thin: the server owns every
decision that matters (seed, score, stars, rewards, lives) and the client is a renderer that
records what the player did and asks the server to judge it. The presentation layer, on the other
hand, is meant to feel like a premium casual mobile puzzle: procedural art, a small tween library,
particles and synthesised sound on every interaction — still with no prefabs, sprites or audio
files in the repository.

## Try it in two minutes

Three ways to see the game running, from zero setup to a real phone:

1. **Offline demo — nothing needed.** Open `unity-client` in Unity 6000.3.10f1 (the editor opens
   `Assets/Scenes/Main.unity` by itself on a fresh clone, or use *BlastScale > Open Main Scene*),
   press Play and tap **Offline demo**. Levels, coins, lives and the leaderboard are simulated on the
   device by the same engine the server uses; nothing is sent anywhere.
2. **Same machine — the real backend.** Run `docker compose up` in the repository root, keep the
   server URL at `http://localhost:8080` and tap **Play as guest** (or register an account). The
   connection status pill under the URL field tells you when the server is up: it turns green
   ("Connected · localhost:8080") as soon as `GET /actuator/health/liveness` answers, and amber
   ("No server at localhost:8080", with a *Retry* button) while it does not.
3. **Phone on the same Wi-Fi.** Type the computer's IP into the URL field (`192.168.1.20:8080`
   works, the scheme is added for you), watch the pill turn green, then play as guest. Building and
   installing on an iPhone is described in the root README ("Playing on an iPhone").

The **?** button on the login screen shows the same three paths inside the game. Mistakes are
explained where they happen: an unreachable server opens a dialog with *Offline demo* / *Retry*,
form problems appear in red next to the field (Register stays disabled until the username is 3–32
letters, digits or `_` and the password 8–72 characters), and the server's `USERNAME_TAKEN`,
`INVALID_CREDENTIALS`, `VALIDATION_ERROR` and `RATE_LIMITED` codes are turned into plain
sentences. *BlastScale > Reset local save* in the editor menu wipes the offline progress and every
stored preference (server URL, toggles, remembered username) when you want a clean run.

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

* `Assets/Scripts/Editor/EditorOnboarding.cs` opens `Main.unity` automatically when the editor starts on an
  empty untitled scene (a fresh clone) and checks that the scene is in the build settings; the *BlastScale*
  menu also has *Open Main Scene* and *Reset local save*.
* Press Play in `Main.unity`. The whole UI (canvas, screens, board) is built from code at runtime with
  UGUI — no prefabs, no TextMeshPro. Textures (rounded cards, shadows, blocks, icons) are generated with
  `Texture2D` at startup and cached; sounds are synthesised into `AudioClip`s. The only binary assets are
  the three font files below.

## Pointing the client at a server

The base URL is resolved in this order (`Assets/Scripts/Net/ClientConfig.cs`):

1. the value typed into the *Server URL* field of the login screen, stored in `PlayerPrefs`
   (`blastscale.baseUrl`) — clear the field (or enter the default) to forget it. Input is normalised:
   whitespace and trailing slashes are dropped and `host:port` without a scheme becomes `http://host:port`;
2. `Assets/Resources/server-config.json` with `{"baseUrl": "https://..."}` — optional, meant to be
   generated at build time for device builds (it is not part of the repository);
3. `http://localhost:8080`.

All endpoints live under `/api/v1` (`Assets/Scripts/Net/ApiRoutes.cs`). Start the backend with
`docker compose up --build` in the repository root (see the root README). The login screen probes
`{baseUrl}/actuator/health/liveness` (`Assets/Scripts/Core/ServerHealth.cs`, 3 s timeout, no token)
when it opens and ~600 ms after the URL field changes, and shows the result in the status pill.

One Unity rule to know: with *Player Settings > Other Settings > Allow downloads over HTTP* at its
default "Not allowed", `UnityWebRequest` refuses plain `http://` to any host except localhost before
sending anything. The pill shows "http to <host> is blocked by Unity" in that case (and `ApiClient`
turns the refusal into a readable error instead of a dead coroutine). `build-ios.sh` sets the option
to "Always allowed" for the phone build; set it yourself to reach a server on another machine from
the editor, or use https.

## Offline demo

The login screen has an **Offline demo** button. It swaps the HTTP client for
`Assets/Scripts/Net/Offline/OfflineApiClient.cs`, a local stand-in that answers every route of the
API without a network:

* levels are generated with the server's `ProceduralLevelGenerator` formula
  (`OfflineLevelGenerator.cs`: 4/5/6 colours by level, `moveLimit = max(14, 20 - level/12)`, target from
  the expected points per move, star thresholds at 1x / 1.25x / 1.5x);
* completions are validated exactly like on the server: the recorded moves are replayed through the
  shared engine (`BoardEngine.Simulate`) and rejected with the same error codes
  (`INVALID_MOVE_SEQUENCE`, `OBJECTIVE_NOT_REACHED`, `SCORE_MISMATCH`);
* coins, lives (30 minute regeneration), boosters, best scores, daily-reward streak, weekly
  leaderboard score and rocket-race points live in `PlayerPrefs` (`blastscale.offline.save`);
* a permanent "Double Reward Weekend" and a "Rocket Race" event are active so the result screen can
  show the reward tag and event points; the leaderboard and event standings contain a few bots;
* `Idempotency-Key` replay and `ALREADY_PROCESSED` responses behave like the real server.

The home screen shows an **OFFLINE DEMO** badge while it is active; *Logout* returns to the online
client. Nothing from the demo is ever sent to a server.

## Screen flow

```
Login ──(guest / login / register / offline demo)──► Home ──► Gameplay ──► Result ──► (Next level) Gameplay
                                                     │  ▲                                │
                                                     │  └────────────── Home ◄───────────┘
                                                     ├──► Shop         (boosters, life refill)
                                                     ├──► Leaderboard  (weekly top 50 + own rank)
                                                     └──► Events       (live events, rocket race standings)
```

* **Login** — `POST /auth/guest` with the device id (or username/password login/register). The bearer
  token is kept in memory (`GameState`) and sent as `Authorization: Bearer <token>`.
* **Home** — `GET /players/me`, `GET /config`, `GET /economy/daily-reward`, `GET /levels/{n}`. Level badge,
  coin / star / life counters with a local regeneration countdown (from `nextLifeInSeconds`, refreshed
  from the server when it reaches zero), the big *Play* button, cards for the daily reward (glowing
  when claimable), shop, leaderboard and events, plus the music / sound toggles. Daily reward claims are
  `POST /economy/daily-reward` with an `Idempotency-Key` and end in a coin burst.
* **Gameplay** — `POST /levels/{n}/start` consumes a life and returns a seed + board rules. The client
  builds the board locally (`BoardState(config, seed)`) and records every move. Tapping a group pops
  it with particles and a score popup, survivors slide down and new blocks drop in (all computed from
  the engine's before/after snapshots). Big groups show a banner ("Great!", "Awesome!",
  "Unstoppable!"); the score counts up, the target bar fills and the stars light up as thresholds are
  crossed. Boosters: Hammer (blocks wiggle while it is armed), Shuffle, +5 Moves (once per attempt).
  *Finish* bounces in when the target is reached; running out of moves auto-submits a win or, after
  offering the +5 Moves booster, a loss.
* **Result** — shows what the **server** answered to `POST /levels/{n}/complete` (score, stars, reward
  strategy tag such as "Double Reward Weekend x2", event points like "+1 Rocket", wallet) with confetti,
  stars popping in one by one and coins flying into the wallet counter, or the loss card after
  `POST /levels/{n}/fail`.
* **Shop** — prices from remote config (`boosterPrices`, `lifeRefillPrice`); `POST /economy/shop/boosters`
  and `POST /economy/shop/lives`, each with an `Idempotency-Key`.
* **Leaderboard** / **Events** — `GET /leaderboards/weekly?limit=50` and `GET /events`.

Errors use the backend's uniform body `{code, message, details, timestamp, path}`: `message` is shown
to the player, `code` drives behaviour (`NO_LIVES_LEFT` opens a "next life in …" dialog with a shop
shortcut, `IDEMPOTENT_REQUEST_IN_PROGRESS` is retried after a second, a 401 sends the player back to
the login screen). Connection failures and 5xx answers open a modal dialog; business errors are
toasts. A dropped connection is retried once with the **same** `Idempotency-Key`.

## Presentation layer

* **Layout** — portrait, 1080x1920 reference resolution, `CanvasScaler` scale-with-screen-size (match 0.5),
  a `SafeAreaFitter` container for the notch / home indicator, 60 fps target, touch-sized buttons
  (130 reference px). The canvas is screen-space-camera so tests can render it into a texture.
* **Art** (`Assets/Scripts/UI/Gfx`) — `SpriteFactory` builds 9-sliced rounded rectangles, drop and inner
  shadows, bevels, circles, spinner arcs and gradients from signed-distance functions; `IconFactory`
  rasterises the icons (coin, heart, star, rocket, trophy, hammer, shuffle, bolt, gift, bag, flag,
  glyphs) with 4x4 supersampling; `BlockSprites` bakes one block texture per colour (gradient, gloss,
  outline, shadow). Colours and sizes live in `UiTheme`; block colours are coral, amber, lime, sky,
  violet and pink.
* **Motion** (`Assets/Scripts/UI/Fx`) — `Tween` is a small pooled tween runner (float/scale/move/fade/tint,
  punch, shake, pulse, pop-out, delays, loops, easing curves in `Ease`); `UiParticles` is a pooled UI
  particle system (bursts, sparkles, confetti, flying coins, score popups); `BokehBackground` draws the
  gradient with drifting bokeh discs; `ScreenManager` slides or fades between screens; `ButtonJuice`
  gives every button the press/release animation, the click sound and the disabled look.
* **Haptics** (`Assets/Scripts/Core/Haptics.cs`) — on iOS the Taptic Engine through the native plugin
  `Assets/Plugins/iOS/BlastScaleHaptics.mm` (prepared `UIImpactFeedbackGenerator`s for light / medium /
  heavy / soft / rigid, a notification generator and a selection generator, iOS 13+); on Android
  `android.os.Vibrator` with amplitude-controlled one-shots (API 26+, plain `vibrate(ms)` below — the
  build needs the `VIBRATE` permission); a no-op in the editor. Button presses tick, block pops thump
  light / medium / heavy by group size (< 5, 5–7, 8+), invalid taps buzz, stars and the target-reached
  moment, wins, losses, daily rewards, purchases and booster use each have their own pattern. Events are
  throttled to ~25 per second and coin ticks are coalesced. The home screen has a haptics toggle next to
  the music and sound ones (`blastscale.haptics`).
* **Board** (`Assets/Scripts/UI/Board`) — `BoardView` lays the blocks out itself and animates pops,
  gravity, refills and shuffles from engine snapshots; `BlockView` is one pooled block.

## Sound and music

Everything in `Assets/Scripts/Audio` is synthesised at startup (`SoundSynth`): UI click, block pop
(pitch rises with the group size), invalid buzz, transition whoosh, coin ticks and bursts, star chime,
win jingle, lose sting, combo swell, booster sound, and a 16 second ambient loop (pad chords
I–vi–IV–V with an arpeggio). `AudioManager` plays effects through a pool of `AudioSource`s (music
0.35, effects 0.8). The home screen has a music toggle and a sound toggle; the choices are stored in
`PlayerPrefs` (`blastscale.music`, `blastscale.sfx`).

Real music is optional: drop `music_main`, `jingle_win` and/or `jingle_lose` (`.ogg`, `.mp3` or `.wav`) into
`Assets/Resources/Audio/` and `AudioManager` uses them instead of the synthesised loop (played at 0.45)
and jingles; the UI and gameplay effects stay synthesised. While a file jingle plays the music ducks to
40 % for two seconds and eases back. The loop starts once at boot and keeps playing through home,
gameplay and result; muting pauses it and unmuting resumes at the same position. Without the files the
behaviour is unchanged.

## Fonts and licenses

`Assets/Fonts/Resources` contains the three TrueType fonts used through legacy UGUI `Text` (dynamic
fonts, loaded with `Resources.Load`, see `UiFonts.cs`):

* **Fredoka One** (`FredokaOne-Regular.ttf`) — titles, scores and numbers;
* **Poppins SemiBold** and **Poppins Regular** (`Poppins-SemiBold.ttf`, `Poppins-Regular.ttf`) — labels and text.

All three are licensed under the SIL Open Font License 1.1; the license texts are next to them
(`Assets/Fonts/OFL-FredokaOne.txt`, `Assets/Fonts/OFL-Poppins.txt`). If a font file is missing the
client falls back to Unity's built-in `LegacyRuntime.ttf` and logs a warning.

## How the client relates to the server

* The gameplay engine in `Assets/Scripts/Engine/` is a line-by-line port of the server's
  `level/engine` package (`SeededRandom`, `BoardConfig`, `Move`, `MoveType`, `BoardState`, `BoardEngine`,
  `SimulationResult`). It is pure C# (`noEngineReferences: true` in its asmdef) so it runs identically on
  both sides: same 32-bit LCG, same row-major fill, same 4-connected pop, gravity, refill and
  "ensure playable" regeneration.
* When a level starts, the server chooses the seed. The client only renders the board that seed
  produces and records the player's moves (`TAP`, `HAMMER`, `SHUFFLE` with row/col) plus whether the
  +5 Moves booster was used. Animations are purely visual: the engine is applied synchronously on
  every tap and the view animates the difference between the snapshots before and after.
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
Assets/Fonts/               Fredoka One + Poppins (Resources/) and their OFL licenses
Assets/Scripts/Engine/      pure C# engine port (asmdef BlastScale.Engine, no UnityEngine)
Assets/Scripts/Net/         IApiClient, ApiClient (UnityWebRequest + Newtonsoft), ApiRoutes, ClientConfig, Dto/*
Assets/Scripts/Net/Offline/ OfflineApiClient, OfflineLevelGenerator, OfflineSave (the offline demo)
Assets/Scripts/Core/        GameState, LevelSession, GameFlow, AppContext, GameBootstrap (the scene's only component),
                            ServerHealth (liveness probe), Haptics (iOS/Android feedback wrapper)
Assets/Plugins/iOS/         BlastScaleHaptics.mm (UIKit feedback generators, compiled by Xcode)
Assets/Scripts/Audio/       SoundSynth (procedural clips), AudioManager
Assets/Scripts/UI/          UiFactory, UiTheme, UiFonts, UiScreen, ScreenManager, Toast, ModalDialog, LoadingOverlay,
                            ButtonJuice, SafeAreaFitter, Screens/*, Board/* (BoardView), Fx/* (Tween, particles, bokeh),
                            Gfx/* (SpriteFactory, IconFactory, BlockSprites)
Assets/Scripts/Editor/      SceneBuilder (generates Main.unity), EditorOnboarding (opens it, menu items), IosBuild
Assets/Tests/Editor/        EngineVectorTests + engine-vectors.json (parity with the Java engine), OnboardingLogicTests
Assets/Tests/PlayMode/      SceneSmokeTests (boot + full offline level), UiScreenshotTests, OnboardingScreenshotTests,
                            GameplayVideoTests (recorder), TestDriver
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

`OnboardingLogicTests.cs` covers the URL normalisation, the form rules and the error-code copy of the
login screen.

The PlayMode tests (`-testPlatform PlayMode`) boot `Main.unity`, check the login screen (including the
connection status pill and the help button), then play a whole level through the offline demo
(login → home → gameplay with animated taps → result → home) and verify the music loop is never
restarted. `OnboardingScreenshotTests` renders the login screen with the pill in both states (the
configured server, then `http://localhost:9`), the "could not reach the server" dialog, the inline
validation, the help dialog and the home toggles into `/tmp/blastscale-onboarding/*.png`.
`UiScreenshotTests` additionally renders the login, home, gameplay, result, shop, leaderboard and
events screens into `/tmp/blastscale-shots/*.png` at 1080x1920 by pointing the UI camera at a render
texture; run it **without** `-nographics` so the editor can render:

```bash
"/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity" -batchmode \
  -runTests -testPlatform PlayMode -projectPath unity-client -testResults /tmp/unity-playmode.xml -logFile /tmp/unity-playmode.log
```

## Recording the demo video

`Assets/Tests/PlayMode/GameplayVideoTests.cs` is a recorder rather than a test: it walks the offline
demo (login, home, a full level, result, leaderboard, events, shop) and writes one JPG per rendered
frame. `Time.captureFramerate` pins the clock to 30 steps per second, so every frame advances
exactly 1/30 s of animation regardless of how long encoding took — the footage is smooth even though
the recording itself runs slower than real time.

```bash
BLASTSCALE_VIDEO_DIR=/tmp/blastscale-video/frames \
  "/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity" -batchmode \
  -projectPath unity-client -runTests -testPlatform PlayMode \
  -testFilter BlastScale.Tests.GameplayVideoTests \
  -testResults /tmp/video.xml -logFile /tmp/video.log
```

(no `-nographics`: the frames have to be rendered). Then encode with ffmpeg:

```bash
ffmpeg -framerate 30 -i /tmp/blastscale-video/frames/frame_%05d.jpg \
  -c:v libx264 -preset slow -crf 22 -pix_fmt yuv420p -movflags +faststart docs/video/blastscale-demo.mp4
```
