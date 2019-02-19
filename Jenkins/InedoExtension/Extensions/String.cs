namespace Inedo.Extensions.Jenkins.Extensions
{
    internal static class String
    {
        public static string TrimEndsCharacter(this string target, char character) => target?.TrimLeadingCharacter(character).TrimTrailingCharacter(character);
        public static string TrimLeadingCharacter(this string target, char character) => Match(target?.Substring(0, 1), character) ? target.Remove(0,1) : target;
        public static string TrimTrailingCharacter(this string target, char character) => Match(target?.Substring(target.Length - 1, 1), character) ? target.Substring(0, target.Length - 1) : target;

        private static bool Match(string value, char character) => !string.IsNullOrEmpty(value) && value[0] == character;
    }
}
