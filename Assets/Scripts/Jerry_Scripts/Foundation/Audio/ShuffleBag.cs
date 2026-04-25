using System;
using System.Collections.Generic;

namespace JerryScripts.Foundation.Audio
{
    /// <summary>
    /// Shuffle-bag randomiser: draws items in a randomised order, guaranteeing every
    /// item is seen once before any item repeats.
    ///
    /// <para>Use this for audio clip selection so the same clip never plays twice in a row
    /// when multiple variants are available.</para>
    ///
    /// <para><b>Algorithm:</b> Fisher–Yates shuffle applied to a working copy of the item
    /// list each time the bag empties. Items are drawn from the back of the shuffled list
    /// (O(1) removal via swap-and-pop).</para>
    ///
    /// <para><b>Edge cases:</b>
    /// <list type="bullet">
    ///   <item>0 items — <see cref="Next"/> returns <c>default(T)</c> and logs nothing (caller guards).</item>
    ///   <item>1 item — always returns the same item (no shuffle needed).</item>
    ///   <item>Deterministic in tests — inject a seeded <see cref="System.Random"/> via the constructor.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <typeparam name="T">The item type. For audio use, typically <c>UnityEngine.AudioClip</c>.</typeparam>
    /// <remarks>S1-006. Foundation-layer — no Unity engine dependencies.</remarks>
    public sealed class ShuffleBag<T>
    {
        // =====================================================================
        // Private state
        // =====================================================================

        private readonly T[]    _items;
        private readonly List<T> _remaining;
        private readonly Random _rng;

        // =====================================================================
        // Construction
        // =====================================================================

        /// <summary>
        /// Constructs a shuffle bag with the given items and a time-seeded PRNG.
        /// </summary>
        /// <param name="items">Source items. Must not be null. May be empty (Next returns default).</param>
        public ShuffleBag(T[] items) : this(items, new Random()) { }

        /// <summary>
        /// Constructs a shuffle bag with a caller-supplied PRNG.
        /// Use this overload in unit tests to get deterministic sequences.
        /// </summary>
        /// <param name="items">Source items. Must not be null.</param>
        /// <param name="rng">Pseudo-random number generator. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> or <paramref name="rng"/> is null.</exception>
        public ShuffleBag(T[] items, Random rng)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (rng   == null) throw new ArgumentNullException(nameof(rng));

            _items     = (T[])items.Clone();    // defensive copy — source array must not mutate
            _remaining = new List<T>(_items.Length);
            _rng       = rng;

            // Pre-fill on construction so the first Next() call doesn't need a refill branch
            Refill();
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Number of items in the bag (total, not remaining in current pass).
        /// </summary>
        public int Count => _items.Length;

        /// <summary>
        /// Draws the next item from the shuffled sequence.
        /// When all items have been drawn, the bag automatically refills and reshuffles.
        ///
        /// <para>Returns <c>default(T)</c> (typically <c>null</c> for reference types) if the
        /// bag was constructed with zero items.</para>
        /// </summary>
        public T Next()
        {
            if (_items.Length == 0) return default;

            if (_remaining.Count == 0)
                Refill();

            // Draw from the end — O(1) removal
            int lastIndex = _remaining.Count - 1;
            T   item      = _remaining[lastIndex];
            _remaining.RemoveAt(lastIndex);
            return item;
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        /// <summary>Copies all items into the working list and applies Fisher–Yates shuffle.</summary>
        private void Refill()
        {
            _remaining.Clear();
            _remaining.AddRange(_items);

            // Fisher–Yates (Knuth) shuffle — O(n)
            for (int i = _remaining.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(0, i + 1);
                T   temp        = _remaining[i];
                _remaining[i]   = _remaining[j];
                _remaining[j]   = temp;
            }
        }
    }
}
