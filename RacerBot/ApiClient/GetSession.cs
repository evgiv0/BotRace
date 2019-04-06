using ServiceStack;

namespace RacerBot.ApiClient
{
    [Route("/raceapi/race", verbs: "GET")]
    public class GetSession : IReturn<PlayerSessionInfo>
    {
        public string SessionId { get; set; }
    }
}