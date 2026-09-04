using System;

namespace BlastScale.Engine
{
    /// <summary>
    /// Tiny deterministic PRNG: a 32-bit linear congruential generator with the Numerical Recipes
    /// constants. Port of the server's <c>SeededRandom.java</c>.
    ///
    /// It is intentionally trivial so that the exact same sequence is reproducible on the server
    /// (Java), here (C#) and in the k6 load-test scripts (JavaScript): server and client must agree
    /// on every board a seed produces, otherwise the server-side replay would reject honest players.
    ///
    /// <code>
    ///   state = (state * 1664525 + 1013904223) mod 2^32
    ///   nextInt(bound) = (state >>> 8) % bound        // the high bits have the better distribution
    /// </code>
    /// </summary>
    public sealed class SeededRandom
    {
        private uint _state;

        /// <summary>
        /// Seeds the generator. Java does <c>seed &amp; 0xFFFFFFFFL</c>, i.e. it keeps the unsigned
        /// 32-bit pattern of the seed; the unchecked cast to <c>uint</c> is the same operation.
        /// </summary>
        public SeededRandom(int seed)
        {
            _state = unchecked((uint)seed);
        }

        /// <summary>Returns a value in <c>[0, bound)</c>; identical to the Java implementation.</summary>
        public int NextInt(int bound)
        {
            if (bound <= 0)
            {
                // Java would fail with an ArithmeticException for 0 and the engine never asks for a
                // non-positive bound, so refusing loudly keeps the ports equivalent.
                throw new ArgumentOutOfRangeException(nameof(bound), "bound must be positive");
            }
            // uint arithmetic wraps at 2^32, which is exactly the "& 0xFFFFFFFF" of the Java code.
            _state = unchecked(_state * 1664525u + 1013904223u);
            return (int)((_state >> 8) % (uint)bound);
        }
    }
}
