using System;
using System.Collections.Generic;
using System.Linq;
using AirGestureAI.Utilities;

namespace AirGestureAI.Security
{
    /// <summary>
    /// Holds the results of a structured input validation check.
    /// </summary>
    public sealed class InputValidationResult
    {
        /// <summary>Gets or sets whether the input passed validation.</summary>
        public bool IsValid { get; set; } = true;

        /// <summary>Gets or sets the error message, if validation failed.</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>Gets or sets the character token that triggered the violation.</summary>
        public string InvalidToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// Detects and rejects shell and command injection character payloads in input buffers.
    /// </summary>
    public static class InputSecurity
    {
        private static readonly char[] InjectionBlacklist = { ';', '&', '|', '>', '<', '$', '`', '\'', '"', '\\' };

        /// <summary>
        /// Scans the given input string for blacklisted injection tokens.
        /// </summary>
        /// <param name="input">The input buffer to validate.</param>
        /// <param name="allowedTokens">Optional tokens from the blacklist that are explicitly allowed in this context.</param>
        /// <returns>A structured <see cref="InputValidationResult"/>.</returns>
        public static InputValidationResult Validate(string input, IEnumerable<char>? allowedTokens = null)
        {
            if (string.IsNullOrEmpty(input))
            {
                return new InputValidationResult { IsValid = true };
            }

            var allowedList = allowedTokens != null ? allowedTokens.ToList() : new List<char>();
            var activeBlacklist = InjectionBlacklist.Where(c => !allowedList.Contains(c)).ToArray();

            foreach (var ch in activeBlacklist)
            {
                if (input.Contains(ch))
                {
                    var msg = $"Command injection check failed. Found blacklisted character '{ch}'.";
                    Logger.Warn($"InputSecurity: {msg} Input sample: '{input.Substring(0, Math.Min(input.Length, 30))}...'");
                    return new InputValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = msg,
                        InvalidToken = ch.ToString()
                    };
                }
            }

            return new InputValidationResult { IsValid = true };
        }
    }
}
