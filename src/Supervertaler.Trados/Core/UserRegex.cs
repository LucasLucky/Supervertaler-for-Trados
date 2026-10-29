using System;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// The time limit for a regular expression the user (or an AI, through the
    /// MCP server's find_and_replace) typed: SuperSearch's search, live filter,
    /// Replace and Replace All, and find_and_replace. Some patterns, such as
    /// <c>(a+)+$</c>, take exponentially long on the wrong text, and a match
    /// cannot be interrupted from outside; until 203 one such pattern froze
    /// Trados Studio for as long as it ran.
    ///
    /// <para>Every such Regex is built with <see cref="MatchTimeout"/>. A match
    /// that runs out of time throws <see cref="System.Text.RegularExpressions.RegexMatchTimeoutException"/>,
    /// which is never read as "no match": each catch that turns a bad pattern
    /// into "no match" or "skip this file" lets it through, and the feature
    /// stops and says <see cref="TooSlow"/>.</para>
    /// </summary>
    internal static class UserRegex
    {
        /// <summary>How long one match may run. Ordinary patterns take
        /// microseconds on a segment.</summary>
        internal static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

        internal const string TooSlow =
            "The regular expression took too long to run (over 2 seconds on one segment or entry) – simplify it. " +
            "Nested repeats such as (a+)+ are the usual cause.";
    }
}
