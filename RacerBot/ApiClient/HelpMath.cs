using ServiceStack;

namespace RacerBot.ApiClient
{
    [Route("/raceapi/help/math", verbs: "GET")]
    public class HelpMath : IReturn<MathResult>
    { }
}