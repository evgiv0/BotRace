using ServiceStack;

namespace RacerBot.ApiClient
{
    [Route("/raceapi/race", verbs: "POST")]
    public class Play : IReturn<PlayerSessionInfo>
    {
        public string Map { get; set; }
    }
}