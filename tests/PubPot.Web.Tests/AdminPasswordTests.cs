using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PubPot.Web.Admin;

namespace PubPot.Web.Tests;

/// <summary>Passwort-Hash des Admin-Kontos, Hilfsbefehl "hash-password" und Prüfung der Einstellungen beim Start.</summary>
public class AdminPasswordTests
{
    private const string Password = "ein-langes-testpasswort";

    [Fact]
    public void Hash_accepts_the_right_password_and_rejects_others()
    {
        var hash = AdminPassword.Hash(Password);

        Assert.True(AdminPassword.Verify(hash, Password));
        Assert.False(AdminPassword.Verify(hash, Password + "x"));
        Assert.False(AdminPassword.Verify(hash, ""));
        Assert.DoesNotContain(Password, hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Same_password_gives_different_hashes_because_of_the_salt()
    {
        Assert.NotEqual(AdminPassword.Hash(Password), AdminPassword.Hash(Password));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("kein-base64!")]
    [InlineData("AQAAAAIAAYag")] // V3-Anfang, aber abgeschnitten
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // falsches Format
    public void Malformed_hash_is_detected_and_never_matches(string hash)
    {
        Assert.False(AdminPassword.IsWellFormedHash(hash));
        Assert.False(AdminPassword.Verify(hash, Password));
    }

    [Fact]
    public void Command_with_piped_input_prints_only_a_working_hash()
    {
        var (exit, output, error) = RunCommand(interactive: false, Password);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.True(AdminPassword.Verify(output.Trim(), Password));
    }

    [Fact]
    public void Command_in_terminal_asks_twice_and_prints_the_environment_variable()
    {
        var (exit, output, _) = RunCommand(interactive: true, Password, Password);

        Assert.Equal(0, exit);
        var line = output.Split('\n').Select(l => l.Trim()).Single(l => l.StartsWith("Admin__PasswordHash=", StringComparison.Ordinal));
        Assert.True(AdminPassword.Verify(line["Admin__PasswordHash=".Length..], Password));
    }

    [Fact]
    public void Command_rejects_short_password()
    {
        var (exit, output, error) = RunCommand(interactive: false, "zu-kurz");

        Assert.Equal(1, exit);
        Assert.Empty(output);
        Assert.Contains("mindestens 12 Zeichen", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Command_rejects_different_repetition()
    {
        var (exit, output, error) = RunCommand(interactive: true, Password, Password + "x");

        Assert.Equal(1, exit);
        Assert.DoesNotContain("Admin__PasswordHash", output, StringComparison.Ordinal);
        Assert.Contains("stimmen nicht überein", error, StringComparison.Ordinal);
    }

    [Fact]
    public void App_starts_without_admin_and_with_a_valid_hash()
    {
        using var withoutAdmin = new PubPotWebFactory().WithWebHostBuilder(b => b.UseSetting("Admin:PasswordHash", ""));
        Assert.False(withoutAdmin.Services.GetRequiredService<IOptions<AdminOptions>>().Value.IsEnabled);

        using var withAdmin = new PubPotWebFactory().WithWebHostBuilder(b =>
            b.UseSetting("Admin:PasswordHash", AdminPassword.Hash(Password)));
        Assert.True(withAdmin.Services.GetRequiredService<IOptions<AdminOptions>>().Value.IsEnabled);
    }

    [Theory]
    [InlineData("Admin:PasswordHash", "abgeschnitten")]
    [InlineData("Admin:Username", " ")]
    public void App_does_not_start_with_broken_admin_settings(string key, string value)
    {
        using var factory = new PubPotWebFactory().WithWebHostBuilder(b => b.UseSetting(key, value));

        var exception = Assert.Throws<OptionsValidationException>(() => factory.Services);
        Assert.Contains("hash-password", exception.Message, StringComparison.Ordinal);
    }

    private static (int Exit, string Output, string Error) RunCommand(bool interactive, params string[] lines)
    {
        using var input = new StringReader(string.Join('\n', lines));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = HashPasswordCommand.Run(new HashPasswordCommand.TextReaderSource(input), output, error, interactive);
        return (exit, output.ToString(), error.ToString());
    }
}
