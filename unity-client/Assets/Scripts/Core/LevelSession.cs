using System;
using System.Collections.Generic;
using BlastScale.Client.Net;
using BlastScale.Client.Net.Dto;
using BlastScale.Engine;
using UnityEngine;

namespace BlastScale.Client.Core
{
    /// <summary>
    /// One attempt at a level: the server's start response, the local board built from its seed,
    /// and every move the player made. The client only *renders* the engine; the server replays
    /// <see cref="Moves"/> on its own copy and decides score, stars and reward.
    /// </summary>
    public sealed class LevelSession
    {
        public LevelStartResponse Start { get; }
        public BoardConfig Config { get; }
        public BoardState Board { get; }

        /// <summary>Every legal move, in order — the payload of /complete and /fail.</summary>
        public List<Move> Moves { get; } = new List<Move>();

        /// <summary>Whether the EXTRA_MOVES booster was activated (allowed once per attempt).</summary>
        public bool ExtraMovesUsed { get; private set; }

        /// <summary>
        /// Idempotency-Key of the completion request, created once per attempt: a retry after a lost
        /// response reuses it, so the server replays the stored result instead of rewarding twice.
        /// </summary>
        public string CompletionKey { get; } = ApiClient.NewIdempotencyKey();

        /// <summary>Realtime clock when the start response arrived (for the local pacing guard).</summary>
        public float StartedAtRealtime { get; }

        public LevelSession(LevelStartResponse start)
        {
            if (start == null || start.board == null)
            {
                throw new ArgumentException("start response has no board");
            }
            Start = start;
            Config = start.board.ToEngineConfig();
            Board = new BoardState(Config, start.seed);
            StartedAtRealtime = Time.realtimeSinceStartup;
        }

        public int Level => Start.level;
        public string SessionId => Start.sessionId;
        public int Score => Board.Score;
        public int TargetScore => Config.TargetScore;
        public bool ObjectiveReached => Board.ObjectiveReached;

        /// <summary>Move limit including the +5 of the EXTRA_MOVES booster when activated.</summary>
        public int EffectiveMoveLimit => Config.MoveLimit + (ExtraMovesUsed ? BoardConfig.ExtraMovesBonus : 0);

        public int MovesLeft => Math.Max(0, EffectiveMoveLimit - Board.MovesUsed);
        public bool OutOfMoves => Board.MovesUsed >= EffectiveMoveLimit;

        /// <summary>Stars the current score would earn (0 until the objective is reached, like the server).</summary>
        public int Stars => ObjectiveReached ? Config.StarsFor(Score) : 0;

        /// <summary>Number of TAP moves so far (boosters do not count as moves).</summary>
        public int TapCount
        {
            get
            {
                int count = 0;
                foreach (Move move in Moves)
                {
                    if (move.Type == MoveType.TAP) count++;
                }
                return count;
            }
        }

        /// <summary>Applies a move to the local board and records it when legal.</summary>
        /// <returns>null when legal, otherwise the engine's reason</returns>
        public string Apply(Move move)
        {
            string problem = Board.Apply(move, EffectiveMoveLimit);
            if (problem == null)
            {
                Moves.Add(move);
            }
            return problem;
        }

        /// <summary>Activates the EXTRA_MOVES booster; false when it was already used.</summary>
        public bool ActivateExtraMoves()
        {
            if (ExtraMovesUsed)
            {
                return false;
            }
            ExtraMovesUsed = true;
            return true;
        }

        /// <summary>Sanity check before submitting: the pure engine must agree with the live board.</summary>
        public SimulationResult Simulate()
        {
            return BoardEngine.Simulate(Config, Start.seed, Moves, ExtraMovesUsed);
        }

        public LevelCompleteRequest ToCompleteRequest()
        {
            return new LevelCompleteRequest
            {
                sessionId = SessionId,
                score = Score,
                movesUsed = Board.MovesUsed,
                moves = MoveDto.From(Moves),
                extraMovesUsed = ExtraMovesUsed
            };
        }

        public LevelFailRequest ToFailRequest()
        {
            return new LevelFailRequest
            {
                sessionId = SessionId,
                moves = MoveDto.From(Moves),
                extraMovesUsed = ExtraMovesUsed
            };
        }
    }
}
