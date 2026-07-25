# ADR 0006: The server replays moves; the client never decides the outcome

## Context
A client that sends `{"coinsEarned": 9999999}` must not be believed. Most casual games validate
plausibility only; we wanted the strongest guarantee that is still cheap.

## Decision
- The board is generated from a server-chosen seed with a tiny deterministic engine
  (32-bit LCG + blast rules) that is implemented three times: Java (server), C# (Unity client)
  and JavaScript (k6 load test). `docs/engine/engine-vectors.json` holds golden cases that every
  port must reproduce bit for bit.
- The client submits its **moves**, not its result. `ReplayValidator` replays them; the
  server's score is the only score that counts.
- Cheaper checks run first in a Chain of Responsibility (`CompletionValidationChain`):
  session ownership/state, level unlocked, implausible speed, move/score bounds, booster
  ownership, then the replay.

## Consequences
- Score inflation, fabricated results and replaying somebody else's session are impossible;
  what remains (a bot playing well) is a fairness question, not an integrity one.
- New rules (device fingerprints, speed profiles per level) are new validator classes; the
  orchestration in `ProgressionService` does not change.
- The engine must stay deterministic across languages: floating point, unsigned arithmetic and
  iteration order are pinned down in the engine's documentation.
