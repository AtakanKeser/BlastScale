using System.Collections.Generic;
using System.IO;
using System.Text;
using BlastScale.Engine;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BlastScale.Tests
{
    /// <summary>
    /// Parity proof for the engine port: replays every case of <c>engine-vectors.json</c> (generated
    /// by the Java <c>BoardEngine</c>) and asserts that the C# engine produces the same initial board,
    /// the same replay summary and the same final board. If any of these ever diverge the server
    /// would reject legitimate completions, so this is the most important test in the client.
    /// </summary>
    public class EngineVectorTests
    {
        private static string VectorsPath => Path.Combine(Application.dataPath, "Tests", "Editor", "engine-vectors.json");

        private static JObject LoadVectors()
        {
            return JObject.Parse(File.ReadAllText(VectorsPath));
        }

        /// <summary>One NUnit test case per vector so the results XML lists each of the 41 cases.</summary>
        public static IEnumerable<TestCaseData> Cases()
        {
            var cases = (JArray)LoadVectors()["cases"];
            for (int i = 0; i < cases.Count; i++)
            {
                var c = (JObject)cases[i];
                string name = "Case" + i.ToString("D2") + "_seed" + c.Value<long>("seed") +
                              (c.Value<bool>("extraMovesUsed") ? "_extra" : "") +
                              (c.Value<bool>("valid") ? "_valid" : "_invalid");
                yield return new TestCaseData(c).SetName(name);
            }
        }

        [Test]
        public void VectorFile_HasExpectedCaseCount()
        {
            Assert.AreEqual(41, ((JArray)LoadVectors()["cases"]).Count);
        }

        [Test]
        public void Rng_MatchesExpectedSequence()
        {
            var rng = (JObject)LoadVectors()["rng"];
            var random = new SeededRandom(rng.Value<int>("seed"));
            int bound = rng.Value<int>("bound");
            var expected = rng["expected"].ToObject<int[]>();
            var actual = new int[expected.Length];
            for (int i = 0; i < expected.Length; i++)
            {
                actual[i] = random.NextInt(bound);
            }
            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        [TestCaseSource(nameof(Cases))]
        public void Case_ReplaysExactlyLikeTheJavaEngine(JObject c)
        {
            BoardConfig config = ParseConfig((JObject)c["config"]);
            int seed = c.Value<int>("seed");
            bool extraMovesUsed = c.Value<bool>("extraMovesUsed");
            List<Move> moves = ParseMoves((JArray)c["moves"]);

            // 1. The initial board generated from the seed.
            var state = new BoardState(config, seed);
            AssertBoardEquals(ParseBoard((JArray)c["initialBoard"]), state.Snapshot(), "initial board");

            // 2. The replay summary via the stateless facade (what the server calls).
            SimulationResult result = BoardEngine.Simulate(config, seed, moves, extraMovesUsed);
            Assert.AreEqual(c.Value<bool>("valid"), result.Valid, "valid (" + result.RejectionReason + ")");
            Assert.AreEqual(c.Value<int>("finalScore"), result.Score, "finalScore");
            Assert.AreEqual(c.Value<int>("finalMovesUsed"), result.MovesUsed, "finalMovesUsed");
            Assert.AreEqual(c.Value<int>("hammersUsed"), result.HammersUsed, "hammersUsed");
            Assert.AreEqual(c.Value<int>("shufflesUsed"), result.ShufflesUsed, "shufflesUsed");
            Assert.AreEqual(c.Value<bool>("objectiveReached"), result.ObjectiveReached, "objectiveReached");
            Assert.AreEqual(c.Value<int>("stars"), result.Stars, "stars");

            // 3. The final board, replayed step by step on the state (stops at the first illegal move
            //    exactly like the generator did).
            int moveLimit = config.MoveLimit + (extraMovesUsed ? BoardConfig.ExtraMovesBonus : 0);
            foreach (Move move in moves)
            {
                if (state.Apply(move, moveLimit) != null)
                {
                    break;
                }
            }
            AssertBoardEquals(ParseBoard((JArray)c["finalBoard"]), state.Snapshot(), "final board");
            Assert.AreEqual(result.Score, state.Score, "state score must equal the facade score");
        }

        // ------------------------------------------------------------------ helpers

        private static BoardConfig ParseConfig(JObject cfg)
        {
            return new BoardConfig(
                cfg.Value<int>("rows"),
                cfg.Value<int>("cols"),
                cfg.Value<int>("colorCount"),
                cfg.Value<int>("moveLimit"),
                cfg.Value<int>("targetScore"),
                cfg["starThresholds"].ToObject<int[]>());
        }

        private static List<Move> ParseMoves(JArray array)
        {
            var moves = new List<Move>();
            foreach (JToken token in array)
            {
                var type = (MoveType)System.Enum.Parse(typeof(MoveType), token.Value<string>("type"));
                moves.Add(new Move(type, token.Value<int>("row"), token.Value<int>("col")));
            }
            return moves;
        }

        private static int[][] ParseBoard(JArray rows)
        {
            var board = new int[rows.Count][];
            for (int r = 0; r < rows.Count; r++)
            {
                board[r] = rows[r].ToObject<int[]>();
            }
            return board;
        }

        private static void AssertBoardEquals(int[][] expected, int[][] actual, string what)
        {
            Assert.AreEqual(expected.Length, actual.Length, what + ": row count");
            for (int r = 0; r < expected.Length; r++)
            {
                if (!RowsEqual(expected[r], actual[r]))
                {
                    Assert.Fail(what + " differs at row " + r + "\nexpected:\n" + Render(expected) + "\nactual:\n" + Render(actual));
                }
            }
        }

        private static bool RowsEqual(int[] a, int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private static string Render(int[][] board)
        {
            var sb = new StringBuilder();
            foreach (int[] row in board)
            {
                sb.Append(string.Join(" ", row)).Append('\n');
            }
            return sb.ToString();
        }
    }
}
