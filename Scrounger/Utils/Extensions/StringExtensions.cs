namespace Scrounger.Utils.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Extension method to convert the string to Proper Case.
    /// </summary>
    /// <param name="input">The string input.</param>
    /// <returns>The string in Proper Case.</returns>
    public static string ToProperCase(this string input)
    {
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
    }
}