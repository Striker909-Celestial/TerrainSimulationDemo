using System.Numerics;
using Plotly.NET;
using Plotly.NET.TraceObjects;

namespace TerrainGenerator;

public static class Program
{
    public static void Main()
    {
        //generate starting terrain
        Dictionary<Vector2, TerrainPoint> terrain = WorldConfig.GenerateBaseTerrain();
        Console.WriteLine();
        Dictionary <Vector2, Dictionary<string, double>> deposits = new Dictionary<Vector2, Dictionary<string, double>>();
        for (int x = 0; x < WorldConfig.WORLD_SIZE.X; x++)
        {
            for (int y = 0; y < WorldConfig.WORLD_SIZE.Y; y++)
            {
                Vector2 i = new Vector2(x, y);
                deposits.Add(i, new Dictionary<string, double>());
                deposits[i].Add("precipitation", 0);
                deposits[i].Add("runoff", 0);
                deposits[i].Add("sediment", 0);
            }
        }
        Dictionary<Vector2, Cloud> clouds = new Dictionary<Vector2, Cloud>();
        Vector3[,] slopeField = new Vector3[(int) WorldConfig.WORLD_SIZE.X, (int) WorldConfig.WORLD_SIZE.Y];

        //simulation loop
        long time = DateTime.UtcNow.Second + 60 * DateTime.UtcNow.Minute + 3600 * DateTime.UtcNow.Hour;
        for (int i = 0; i < WorldConfig.SIMULATION_LENGTH; i++)
        {
            //refreshes slope field if needed
            if (i % WorldConfig.SLOPE_FIELD_REFRESH_RATE == 0)
            {
                slopeField = WorldConfig.GenerateSlopeFieldFromTerrain(terrain);
            }
            
            //terrain ticks
            for (int x = 0; x < WorldConfig.WORLD_SIZE.X; x++)
            {
                for (int y = 0; y < WorldConfig.WORLD_SIZE.Y; y++)
                {
                    Vector2 p = new Vector2(x, y);
                    Vector3 slope = slopeField[x, y];
                    //Console.Write(terrain[p].Position.Z);
                    Dictionary<string, double> outputs = terrain[p].Tick(slope.Z, 
                        deposits[p]["precipitation"],
                        deposits[p]["runoff"],
                        deposits[p]["sediment"],
                        WorldConfig.random
                        );
                    //Console.WriteLine(", {0}, {1}", terrain[p].Position.Z, outputs["sediment"]);
                    deposits[p]["precipitation"] = 0;
                    deposits[p]["runoff"] = 0;
                    deposits[p]["sediment"] = 0;
                    
                    Vector2 p2 = p + new Vector2(slope.X, slope.Y);
                    //Console.WriteLine("{0}, {1}, {2}", p, p2, slope);
                    deposits[p2]["runoff"] += outputs["runoff"];
                    deposits[p2]["sediment"] += outputs["sediment"];

                    Cloud c = new Cloud(terrain[p].Position,
                        new Vector3((float)WorldConfig.random.NextDouble(), (float)WorldConfig.random.NextDouble(),
                            (float) WorldConfig.random.NextDouble()),
                        outputs["vapor"],
                        outputs["vapor"] * 10
                    );
                    if (!clouds.TryAdd(p, c))
                    {
                        clouds[p] += c;
                    }
                }
            }
            
            //cloud ticks
            Vector3 wind = WorldConfig.Wind(i);
            Vector2[] cloudKeys = new Vector2[clouds.Keys.Count];
            clouds.Keys.CopyTo(cloudKeys, 0);
            foreach (Vector2 p in cloudKeys)
            {
                Cloud c = clouds[p];
                Dictionary<string, double> outputs = c.Tick(wind, terrain);
                Vector2 p2 = new Vector2((float) outputs["target_x"], (float) outputs["target_y"]);
                deposits[p2]["precipitation"] += outputs["precipitation"];
                if (p2.X >= 0 && p2.Y >= 0 && p2.X < WorldConfig.WORLD_SIZE.X && p2.Y < WorldConfig.WORLD_SIZE.Y)
                {
                    if (!clouds.TryAdd(p2, c))
                    {
                        clouds[p2] += c;
                    }
                }

                clouds.Remove(p);
            }
            
            //update displays
            double progress = i / (double) WorldConfig.SIMULATION_LENGTH;
            Console.Write("\rRunning Water Cycle Simulation: ");
            WorldConfig.PrintProgressBar(progress, 10);
            Console.Write(" {0}% ", Math.Round(progress * 100));
            WorldConfig.PrintTime(DateTime.UtcNow.Second + 60 * DateTime.UtcNow.Minute + 3600 * DateTime.UtcNow.Hour - time);
        }
        Console.Write("\rSimulated {0} Ticks in ", WorldConfig.SIMULATION_LENGTH);
        WorldConfig.PrintTime(DateTime.UtcNow.Second + 60 * DateTime.UtcNow.Minute + 3600 * DateTime.UtcNow.Hour - time);
        Console.WriteLine();

        double[] xAxis = new double[terrain.Count];
        double[] yAxis = new double[terrain.Count];
        double[] zAxis = new double[terrain.Count];
        
        //Color[] vegetation = new Color[terrain.Count];

        time = DateTime.UtcNow.Second + 60 * DateTime.UtcNow.Minute + 3600 * DateTime.UtcNow.Hour;
        int ii = 0;
        for (int x = 0; x < WorldConfig.WORLD_SIZE.X; x++)
        {
            for (int y = 0; y < WorldConfig.WORLD_SIZE.Y; y++)
            {
                TerrainPoint p = terrain[new Vector2(x, y)];
                xAxis[ii] = x;
                yAxis[ii] = y;
                zAxis[ii] = p.Position.Z;
                //vegetation[ii] = Color.fromRGB(0, (int)Math.Max(255 * p.Vegetation, 255), 0);

                //Console.WriteLine(p.Position);
                ii++;
                double progress = ii / (double) WorldConfig.SIMULATION_LENGTH;
                Console.Write("\rRendering Terrain: ");
                WorldConfig.PrintProgressBar(progress, 10);
                Console.Write(" {0}% ", Math.Round(progress * 100));
                WorldConfig.PrintTime(DateTime.UtcNow.Second + 60 * DateTime.UtcNow.Minute + 3600 * DateTime.UtcNow.Hour - time);
            }
        }
        Console.Write("\rRendered Terrain in ");
        WorldConfig.PrintTime(DateTime.UtcNow.Second + 60 * DateTime.UtcNow.Minute + 3600 * DateTime.UtcNow.Hour - time);
        Console.WriteLine();

        var chart = Chart3D.Chart.Mesh3D<double, double, double, int, int, int, string>(x:xAxis, y:yAxis, z:zAxis, Name:"Terrain", TriangulationAlgorithm:StyleParam.TriangulationAlgorithm.Delaunay);
        //var chart1 = Chart3D.Chart.Point3D<double, double, double, string>(x:xAxis, y:yAxis, z:zAxis, Name:"Vegetation", MarkerColor: Color.fromColors(vegetation));
        
        chart.Show();
        //chart1.Show();
    }
}