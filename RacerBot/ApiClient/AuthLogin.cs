using ServiceStack;

namespace RacerBot.ApiClient
{
    [Route("/raceapi/Auth/Login", verbs: "POST")]
    public class AuthLogin : IReturn<TokenResult>
    {
        public string Login { get; set; }
        public string Password { get; set; }
    }
}