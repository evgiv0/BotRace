using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ServiceStack;
using Dijkstra.NET.Graph;
using Dijkstra.NET.ShortestPath;
using RacerBot.ApiClient;

namespace RacerBot
{
    class Program
    {
        const string TestMap = "test";//OK
        const string MazeMap = "maze";
        const string SpinMap = "spin";//OK
        const string LabirintMap = "labirint";//OK
        const string RiftMap = "rift";//OK

        static PlayerStatusEnum Status;
        static DirectionEnum Heading;
        static Cell CurrentCell;
        static Cell TargetCell;
        static int CurrentSpeed;
        static Dictionary<DirectionEnum, DeltaLocation> Deltas { get; set; }
        static Graph<Vector3, string> graph;

        static JsonServiceClient client = new JsonServiceClient("http://51.15.100.12:5000");

        static JsonServiceClient visual = new JsonServiceClient("http://127.0.0.1:5000");

        static string SessionId { get; set; }

        static MathResult math;

        private static int optimalSpeed;

        static Map CurrentMap;

        static Dictionary<Vector3,int> GoneCells = new Dictionary<Vector3,int>();

        static void Main(string[] args)
        {
            TokenResult response;
            while (true)
            {
                try
                {
                    response = client.Post(new AuthLogin { Login = "Bots", Password = "uINKjn" });
                    break;
                }
                catch (Exception)
                {

                }
            }

            client.BearerToken = response.Token;
            visual.BearerToken = response.Token;

            math = client.Get(new HelpMath());
            optimalSpeed = (math.MaxDuneSpeed + math.MinCanyonSpeed) / 2;
            Deltas = math.LocationDeltas.ToDictionary(i => i.Direction, i => i.Delta);
            var sessionInfo =  client.Post(new Play { Map = MazeMap });
            //var sessionInfo = client.Get(new GetSession {SessionId = $"Bots{RiftMap}" });

            SessionId = sessionInfo.SessionId;
            CurrentMap = new Map(sessionInfo.NeighbourCells, sessionInfo.Radius);
            Heading = sessionInfo.CurrentDirection;
            CurrentCell = CurrentMap.GetCell(sessionInfo.CurrentLocation);
            TargetCell = CurrentMap.GetCell(sessionInfo.Finish);

            while (Status != PlayerStatusEnum.HappyAsInsane && Status != PlayerStatusEnum.Punished)
            {
                var heading = Heading;
                var @from = invertedDictionary[CurrentCell.Item1.vector3];
                var to = invertedDictionary[TargetCell.Item1.vector3];
                ShortestPathResult result = graph.Dijkstra(@from, to); //result contains the shortest path

                var fullpath = result.GetPath();
                var path = fullpath.Skip(1).FirstOrDefault();
                var cell = invertedDictionary.FirstOrDefault(pair => pair.Value == path).Key;
                if (cell !=null)
                {
                    var delta =  cell - CurrentCell.Item1.vector3;
                    var direc = Deltas.FirstOrDefault(pair =>
                        pair.Value.Dx == delta.X && pair.Value.Dy == delta.Y && pair.Value.Dz == delta.Z);
                    heading = direc.Key;
                }
                
                //bool driftWarning = false;
                //int driftDown = 0;

                while (true)
                {
                    int accel = 0;
                    var nexCell = CurrentMap.GetCell(CurrentCell.Item1, heading);

                   
                    var isTurn = TurnSolution(nexCell, ref heading, ref accel);
                    
                    if (isTurn)
                    {
                        var angle = Math.Abs((int) heading - (int) Heading);

                        //foreach (var driftsAngle in math.DriftsAngles)
                        //{
                        //    if (driftsAngle.Angle>=angle && driftsAngle.MaxSpeed>=CurrentSpeed)
                        //    {
                        //        driftDown = CurrentSpeed - driftsAngle.MaxSpeed;
                        //        driftWarning = true;
                        //        heading = heading.TurnLeft();
                        //    }
                        //}

                        //if (!driftWarning)
                        //{
                        //    heading = heading.TurnLeft();
                        //}
                        //else
                        {
                            //nexCell = CurrentMap.GetCell(CurrentCell.Item1, heading);
                            Turn(heading, accel);
                        }
                        break;
                    }
                    else
                    {
                        //if (!GoneCells.ContainsKey(nexCell.Item1.vector3))
                        {
                            heading = heading.TurnLeft();
                        }
                        //else
                        //{
                        //    heading = heading.TurnRight();
                        //}
                    }

                }
            }
        }

        private static bool TurnSolution(Cell nexCell, ref DirectionEnum direction, ref int accel)
        {
            switch (nexCell.Item2)
            {
                case CellType.Empty:
                    accel = optimalSpeed - CurrentSpeed;
                    return true;
                case CellType.Rock:
                    return false;
                case CellType.DangerousArea://медлеенно
                    accel = math.MaxDuneSpeed - CurrentSpeed;
                    if (accel < -math.MaxAcceleration)
                    {
                        return false;
                    }
                    return true;
                case CellType.Pit://быстро
                    accel = math.MinCanyonSpeed - CurrentSpeed;
                    if (accel > math.MaxAcceleration)
                    {
                        return false;
                    }
                    return true;
                default: return false;
            }
        }

        public static Dictionary<Vector3, uint> invertedDictionary;

        public class Map
        {
            public Map(List<Cell> cells, int radius)
            {
                for (int x = -radius+1; x < radius; x++)
                {
                    for (int y = -radius+1; y < radius; y++)
                    {
                        var z = -x - y;
                        if ((z > -radius) && (z < radius))
                        {
                            var coord = new Location() { X = x, Y = y, Z = z };
                            var type = CellType.Empty;
                            if (Math.Max(Math.Max(x,y),z) == radius-1 || Math.Min(Math.Min(x, y), z) == -radius + 1)
                            {
                                type = CellType.Rock;
                            }
                            Cells[coord.vector3] = new Cell() { Item1 = coord, Item2 = type };
                        }
                    }
                }

                Update(cells);
            }


            public void Update(List<Cell> cells)
            {
                foreach (Cell item in cells)
                {
                    Cells[item.Item1.vector3] = item;
                }
                graph = new Graph<Vector3, string>();
                invertedDictionary = new Dictionary<Vector3,uint>();
                uint ind = 1;
                foreach (var cell in Cells)
                {
                    graph.AddNode(cell.Value.Item1.vector3);
                    invertedDictionary[cell.Value.Item1.vector3] = ind++;
                }


                foreach (var cell in Cells)
                {
                    if (cell.Value.Item2 == CellType.Rock)
                    {
                        continue;
                    }
                    foreach (DirectionEnum direction in Enum.GetValues(typeof(DirectionEnum)))
                    {
                        var neig = GetCell(cell.Value.Item1, direction);
                        var cost = 2;
                        if (GoneCells.ContainsKey(neig.Item1.vector3))
                        {
                            cost = 2+ GoneCells[neig.Item1.vector3];
                        }
                        switch (neig.Item2)
                        {
                                
                            case CellType.Empty:
                                graph.Connect(invertedDictionary[cell.Value.Item1.vector3], invertedDictionary[neig.Item1.vector3], cost, "");
                                break;
                            case CellType.Rock:
                                break;
                            case CellType.DangerousArea:
                                if (cell.Value.Item2 != CellType.Pit)
                                    graph.Connect(invertedDictionary[cell.Value.Item1.vector3], invertedDictionary[neig.Item1.vector3], cost+1, "");
                                break;
                            case CellType.Pit:
                                if (cell.Value.Item2 != CellType.DangerousArea)
                                    graph.Connect(invertedDictionary[cell.Value.Item1.vector3], invertedDictionary[neig.Item1.vector3], cost+1, "");
                                break;
                        }
                    }
                }

            }

            Dictionary<Vector3, Cell> Cells = new Dictionary<Vector3, Cell>();

            public Cell GetCell(Location location, DirectionEnum? direction = null)
            {
                if (direction == null)
                    return Cells[location.vector3];
                var newcoord = location.vector3 + Deltas[direction.Value].vector3;
                return Cells[newcoord];
            }
        }

        public static TurnResult Turn(DirectionEnum direction, int acceleration)
        {
            var turnResult = client.Put(new SetStep
            {
                SessionId = SessionId,
                Direction = direction.ToString(),
                Acceleration = acceleration
            });

            CurrentMap.Update(turnResult.VisibleCells);
            Status = turnResult.Status;
            Heading = turnResult.Heading;
            CurrentCell = CurrentMap.GetCell(turnResult.Location);
            CurrentSpeed = turnResult.Speed;
            if (GoneCells.ContainsKey(CurrentCell.Item1.vector3))
            {
                GoneCells[CurrentCell.Item1.vector3]++;
            }
            else
            {
                GoneCells[CurrentCell.Item1.vector3]=1;
            }
            visual.Get(new GetSession { SessionId = SessionId });
            return turnResult;
        }
    }

    public class TokenResult
    {
        public string Token { get; set; }
    }

    public class MathResult
    {
        public class DriftsAngle
        {
            public int Angle { get; set; }
            public int MaxSpeed { get; set; }
            public int SpeedDownShift { get; set; }
        }
        public int MaxSpeed { get; set; }
        public int MinSpeed { get; set; }
        public int MaxAcceleration { get; set; }
        public List<DriftsAngle> DriftsAngles { get; set; }

        public int MinCanyonSpeed { get; set; }
        public int MaxDuneSpeed { get; set; }
        public int BaseTurnFuelWaste { get; set; }
        public int DriftFuelMultiplier { get; set; }
        public int FullSpeedFuelMultiplier { get; set; }

        public class AngleType
        {
            public DirectionEnum Direction { get; set; }
            public int Angle { get; set; }
        }

        public List<AngleType> Angles { get; set; }

        public List<LocationDelta> LocationDeltas { get; set; }
    }

    public enum DirectionEnum
    {
        SouthWest = -120,
        SouthEast = -60,
        East = 0,
        NorthEast = 60,
        NorthWest = 120,
        West = 180,
    }

    public static class DirectionExt
    {
        public static DirectionEnum TurnLeft(this DirectionEnum direction)
        {
            switch (direction)
            {
                case DirectionEnum.SouthWest: return DirectionEnum.West;
                case DirectionEnum.SouthEast: return DirectionEnum.SouthWest;
                case DirectionEnum.East: return DirectionEnum.SouthEast;
                case DirectionEnum.NorthEast: return DirectionEnum.East;
                case DirectionEnum.NorthWest: return DirectionEnum.NorthEast;
                case DirectionEnum.West: return DirectionEnum.NorthWest;
                default: return direction;
            }
        }

        public static DirectionEnum TurnRight(this DirectionEnum direction)
        {
            switch (direction)
            {
                case DirectionEnum.SouthWest: return DirectionEnum.SouthEast;
                case DirectionEnum.SouthEast: return DirectionEnum.East;
                case DirectionEnum.East: return DirectionEnum.NorthEast;
                case DirectionEnum.NorthEast: return DirectionEnum.NorthWest;
                case DirectionEnum.NorthWest: return DirectionEnum.West;
                case DirectionEnum.West: return DirectionEnum.SouthWest;
                default: return direction;
            }
        }
    }

    public class LocationDelta
    {
        public DirectionEnum Direction { get; set; }
        public DeltaLocation Delta { get; set; }
    }

    public class DeltaLocation
    {
        public int Dx { get; set; }
        public int Dy { get; set; }
        public int Dz { get; set; }
        public Vector3 vector3 => new Vector3(Dx, Dy, Dz);
    }

    public class Location
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public Vector3 vector3 => new Vector3(X, Y, Z);
    }

    public class Cell
    {
        public Location Item1 { get; set; }
        public CellType Item2 { get; set; }
        public override string ToString()
        {
            return Item2.ToString();
        }
    }

    public enum CellType
    {
        Empty, Rock, DangerousArea, Pit
    }

    public enum PlayerStatusEnum
    {
        NotBad, Drifted, Hungry, Punished, HappyAsInsane
    }

    public class PlayerSessionInfo
    {
        public string SessionId { get; set; }
        public string PlayerId { get; set; }
        public DirectionEnum CurrentDirection { get; set; }
        public Location CurrentLocation { get; set; }
        public Location Finish { get; set; }
        public int Radius { get; set; }
        public int CurrentSpeed { get; set; }
        public PlayerStatusEnum PlayerStatus { get; set; }
        public List<Cell> NeighbourCells { get; set; }
        public int Fuel { get; set; }
    }

    public class Command
    {
        public Location Location { get; set; }
        public int Acceleration { get; set; }
        public DirectionEnum MovementDirection { get; set; }
        public DirectionEnum Heading { get; set; }
        public int Speed { get; set; }
        public int Fuel { get; set; }
    }

    public class TurnResult
    {
        public Command Command { get; set; }
        public List<Cell> VisibleCells { get; set; }
        public Location Location { get; set; }
        public int ShortestWayLength { get; set; }
        public int Speed { get; set; }
        public PlayerStatusEnum Status { get; set; }
        public DirectionEnum Heading { get; set; }
        public int FuelWaste { get; set; }
    }
}
