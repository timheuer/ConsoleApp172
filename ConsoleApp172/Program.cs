using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

class Program
{
    static EmbedIOAuthServer _server;
    static string clientId;
    static string clientSecret;

    static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddUserSecrets<Program>();

        var configuration = builder.Build();

        clientId = configuration["SPOTIFY_CLIENT_ID"];
        clientSecret = configuration["SPOTIFY_CLIENT_SECRET"];
        var redirectUri = "http://localhost:5543/";


        // Generate code verifier and code challenge
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        _server = new EmbedIOAuthServer(
            new Uri("http://localhost:5543/"),
            5543,
            Assembly.GetExecutingAssembly(),
            "Example.CLI.CustomHTML.Resources.custom_site"
          );
        await _server.Start();
        _server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;

        var loginRequest = new LoginRequest(
            _server.BaseUri,
            clientId,
            LoginRequest.ResponseType.Code
        )
        {
            Scope = new[] { Scopes.PlaylistReadPrivate, Scopes.PlaylistReadCollaborative, Scopes.Streaming }
        };

        var uri = loginRequest.ToUri();
        Console.WriteLine($"Please open the following URL in your browser to authenticate:\n{uri}");

        Console.WriteLine("Enter the authorization code:");
        var code = Console.ReadLine();

        // With this line
        //var tokenResponse = await new OAuthClient().RequestToken(
        //    new PKCETokenRequest(clientId, code, new Uri(redirectUri), codeVerifier)
        //);

        //var spotify = new SpotifyClient(tokenResponse.AccessToken);

        //var playlists = await spotify.Playlists.CurrentUsers();

        //await foreach (var item in spotify.Paginate(playlists))
        //{
        //    Console.WriteLine(item.Name);
        //}

        Console.ReadLine();
    }

    private static async Task OnAuthorizationCodeReceived(object arg1, AuthorizationCodeResponse response)
    {
        await _server.Stop();

        var token = await new OAuthClient().RequestToken(
          new AuthorizationCodeTokenRequest(clientId, clientSecret, response.Code, _server.BaseUri)
        );

        var config = SpotifyClientConfig.CreateDefault().WithToken(token.AccessToken, token.TokenType);
        var spotify = new SpotifyClient(config);

        var me = await spotify.UserProfile.Current();

        Console.WriteLine($"Your E-Mail: {me.Email}");
        Environment.Exit(0);
    }

    private static string GenerateCodeVerifier()
    {
        using (var rng = new RNGCryptoServiceProvider())
        {
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using (var sha256 = SHA256.Create())
        {
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
