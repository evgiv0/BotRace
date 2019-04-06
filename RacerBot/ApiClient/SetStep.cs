using ServiceStack;

namespace RacerBot.ApiClient
{
    [Route("/raceapi/race/{sessionId}", verbs: "PUT")]
    public class SetStep : IReturn<TurnResult>
    {
        public string SessionId { get; set; }
        public string Direction { get; set; }
        public int Acceleration { get; set; }
    }
}