namespace KnovaHub.ApplicationLayer.Services;

/// <summary>
/// Reglas de contraseña y generación de contraseñas temporales.
/// </summary>
public static class PasswordPolicy
{
    public static bool IsStrong(string password) =>
        password.Length >= 8
        && password.Any(char.IsDigit)
        && password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));

    /// <summary>
    /// Genera una contraseña temporal que cumple la política (8 letras + 1 dígito + 1 símbolo, mezclados).
    /// </summary>
    public static string GenerateTemporary()
    {
        const string letters = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string symbols = "!@#$%&*";

        var rnd = Random.Shared;
        var chars = new char[10];
        for (var i = 0; i < 8; i++)
            chars[i] = letters[rnd.Next(letters.Length)];
        chars[8] = digits[rnd.Next(digits.Length)];
        chars[9] = symbols[rnd.Next(symbols.Length)];

        // Mezcla para que el dígito y el símbolo no queden siempre al final.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = rnd.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
