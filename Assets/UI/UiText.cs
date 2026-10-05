using System.Globalization;
using System.Text;

namespace DungeonTower.UI
{
    /// <summary>
    /// Small text helpers shared by the character sheet, the stat
    /// tooltips, and the item/ability describer, so numbers and colors
    /// look the same everywhere.
    /// </summary>
    public static class UiText
    {
        public const string UnmetColor = "#E06666";
        public const string HighlightColor = "#FFD54A";

        // 1.5 -> "1.5", 6 -> "6", 0.3333 -> "0.33". Culture-invariant so
        // a comma-decimal locale never turns "1.5" into "1,5".
        public static string Num(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        public static string Signed(float value) => (value > 0f ? "+" : "") + Num(value);

        public static string Unmet(string text) => $"<color={UnmetColor}>{text}</color>";

        public static string Highlight(string text) => $"<color={HighlightColor}>{text}</color>";

        // "SecondWind" -> "Second Wind".
        public static string Humanize(string pascalCase)
        {
            if (string.IsNullOrEmpty(pascalCase)) return "";
            var sb = new StringBuilder(pascalCase.Length + 4);
            for (int i = 0; i < pascalCase.Length; i++)
            {
                char c = pascalCase[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(pascalCase[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
