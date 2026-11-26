using System.Text;

namespace PubPot.Web.Admin;

/// <summary>
/// Hilfsbefehl, der den Hash für <c>Admin__PasswordHash</c> erzeugt:
/// <code>dotnet run --project src/PubPot.Web -- hash-password</code>
/// Im Terminal wird das Passwort verdeckt und zweimal abgefragt. Kommt es über eine Pipe
/// (z. B. später im Container), wird die erste Zeile gelesen. Das Passwort landet so nie in der Shell-Historie.
/// </summary>
public static class HashPasswordCommand
{
    public const string Name = "hash-password";

    /// <summary>Startet den Befehl mit der echten Konsole. Rückgabe ist der Exit-Code.</summary>
    public static int RunWithConsole()
    {
        var interactive = !Console.IsInputRedirected;
        return Run(
            interactive ? new HiddenConsoleReader() : new TextReaderSource(Console.In),
            Console.Out,
            Console.Error,
            interactive);
    }

    public static int Run(IPasswordSource input, TextWriter output, TextWriter error, bool interactive)
    {
        var password = input.ReadPassword(interactive ? "Passwort: " : null);
        if (password is null || password.Length < AdminPassword.MinLength)
        {
            error.WriteLine($"Das Passwort muss mindestens {AdminPassword.MinLength} Zeichen lang sein.");
            return 1;
        }

        if (interactive && input.ReadPassword("Noch einmal: ") != password)
        {
            error.WriteLine("Die beiden Eingaben stimmen nicht überein.");
            return 1;
        }

        var hash = AdminPassword.Hash(password);
        if (interactive)
        {
            output.WriteLine();
            output.WriteLine("Diesen Wert als Umgebungsvariable setzen (in Docker: deploy/.env):");
            output.WriteLine($"  Admin__PasswordHash={hash}");
            output.WriteLine("Lokal stattdessen:");
            output.WriteLine($"  dotnet user-secrets set \"Admin:PasswordHash\" \"{hash}\" --project src/PubPot.Web");
        }
        else
        {
            // Über eine Pipe nur der Hash, damit Skripte ihn direkt weiterverwenden können.
            output.WriteLine(hash);
        }

        return 0;
    }

    /// <summary>Woher das Passwort kommt. Im Test ersetzbar.</summary>
    public interface IPasswordSource
    {
        string? ReadPassword(string? prompt);
    }

    public sealed class TextReaderSource(TextReader reader) : IPasswordSource
    {
        public string? ReadPassword(string? prompt) => reader.ReadLine();
    }

    /// <summary>Liest Tasten ohne Echo, damit das Passwort nicht auf dem Bildschirm steht.</summary>
    private sealed class HiddenConsoleReader : IPasswordSource
    {
        public string? ReadPassword(string? prompt)
        {
            Console.Write(prompt);
            var text = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return text.ToString();
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (text.Length > 0)
                    {
                        text.Length--;
                    }
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    text.Append(key.KeyChar);
                }
            }
        }
    }
}
