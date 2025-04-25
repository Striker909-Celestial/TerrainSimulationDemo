using System.Numerics;

namespace TerrainGenerator;

public static class WorldConfig
{
    public static double SEA_LEVEL = 25.0;
    public static double MAX_HEIGHT = 100.0;
    public static double MIN_HEIGHT = 0.0;

    public static double SEA_LEVEL_TEMP = 15.0;
    public static double TEMP_RANGE = 50.0;
    public static double SEA_LEVEL_PRESSURE = 1.0;
    
    public static Vector2 WORLD_SIZE = new Vector2(500, 500);
    
    public static int SIMULATION_LENGTH = 1000;
    public static int SLOPE_FIELD_REFRESH_RATE = 1;

    private static int _seed = 1234567891;
    public static Random random = new Random(_seed);

    public static double Gaussian(double x, double offset, double std)
    {
        return Math.Exp(-Math.Pow(x - offset, 2) / (2 * std * std));
    }

    public static double TemperatureGradient(Vector3 position3D)
    {
        double geoTemp = Gaussian(position3D.Y, WORLD_SIZE.Y / 2, WORLD_SIZE.Y / 4);
        double heightTemp = 1 - (position3D.Z - MIN_HEIGHT) / (MAX_HEIGHT - MIN_HEIGHT);
        return (0.6 * geoTemp + 0.3 * heightTemp + 0.1 * random.NextDouble()) * TEMP_RANGE  - (TEMP_RANGE / 2 - SEA_LEVEL_TEMP);
    }

    public static double PressureGradient(double height)
    {
        double heightPress = Math.Pow(0.01, (height - MIN_HEIGHT) / (MAX_HEIGHT - MIN_HEIGHT));
        return (0.9 * heightPress + 0.1 * random.NextDouble()) * Math.Pow(100.0, (SEA_LEVEL - MIN_HEIGHT) / (MAX_HEIGHT - MIN_HEIGHT)) * SEA_LEVEL_PRESSURE;
    }

    public static Vector3 Wind(double t)
    {
        return new Vector3(
            (float)(random.NextDouble() * 10 * Math.Sin(0.1 * t) + 3),
            (float)(random.NextDouble() * 10 * Math.Sin(0.1 * t + 1) + 3),
            (float)(random.NextDouble() * 8 * Math.Sin(0.1 * t - 1) + 2)
        );
    }

    public static void PrintProgressBar(double progress, int max)
    {
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.Green;
        for (int i = 0; i < max; i++)
        {
            if ((double)i / (double)max <= progress)
            {
                Console.Write("=");
            }
            else
            {
                Console.Write(" ");
            }
        }
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("]");
    }

    public static void PrintTime(long seconds)
    {
        if (seconds >= 3600)
        {
            Console.Write("{0} hour{1}, ", seconds / 3600, (seconds / 3600 > 1) ? "s" : "");
            seconds %= 3600;
        }

        if (seconds >= 60)
        {
            Console.Write("{0} minute{1}, ", seconds / 60, (seconds / 60 > 1) ? "s" : "");
            seconds %= 60;
        }
        Console.Write("{0} second{1}.                           ", seconds, (seconds > 1) ? "s" : "");
    }

    public static Dictionary<Vector2, TerrainPoint> GenerateBaseTerrain()
    {
        Dictionary<Vector2, TerrainPoint> terrain = new Dictionary<Vector2, TerrainPoint>();
        double max = WORLD_SIZE.X * WORLD_SIZE.Y;
        double count = 0;
        long time = DateTime.UtcNow.Second + 60 * DateTime.UtcNow.Minute + 3600 * DateTime.UtcNow.Hour;
        Console.Write("Generating Base Terrain: [          ] 0%");
        for (int x = 0; x < WORLD_SIZE.X; x++)
        {
            for (int y = 0; y < WORLD_SIZE.Y; y++)
            {
                Vector2 i = new Vector2(x, y);
                float octal1 = IcariaNoise.GradientNoise(x / WORLD_SIZE.X, y / WORLD_SIZE.Y, _seed);
                float octal2 = IcariaNoise.GradientNoise(x / WORLD_SIZE.X / 2, y / WORLD_SIZE.Y / 2, _seed + 1);
                float octal3 = IcariaNoise.GradientNoise(x / WORLD_SIZE.X / 4, y / WORLD_SIZE.Y / 4, _seed + 2);
                float noise = octal1 * 0.9f + octal2 * 0.09f + octal3 * 0.01f;
                float height = (float)((noise + 1.0) / 2.0 * (MAX_HEIGHT - MIN_HEIGHT) + MIN_HEIGHT);
                terrain.Add(i, new TerrainPoint(
                    new Vector3(i, height), 
                    random.NextDouble(), random.NextDouble(), random.NextDouble() * 000.1));
                terrain[i].Groundwater = Math.Max(SEA_LEVEL - terrain[i].Position.Z, 0);
                
                count++;
                double progress = count / max;
                Console.Write("\rGenerating Base Terrain: ");
                PrintProgressBar(progress, 10);
                Console.Write(" {0}% ", Math.Round(progress * 100));
                PrintTime(DateTime.UtcNow.Second + 60 * DateTime.UtcNow.Minute + 3600 * DateTime.UtcNow.Hour - time);
            }
        }
        Console.Write("\rGenerated Base Terrain in ");
        PrintTime(DateTime.UtcNow.Second + 60 * DateTime.UtcNow.Minute + 3600 * DateTime.UtcNow.Hour - time);
        
        return terrain;
    }

    private static Vector3 MaxZ(Vector3[] v)
    {
        Vector3 result = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (Vector3 v3 in v)
        {
            if (v3.Z > result.Z)
            {
                result = v3;
            }
        }
        return result;
    }
    
    private static Vector3 MinZ(Vector3[] v)
    {
        Vector3 result = new Vector3(float.MinValue, float.MaxValue, float.MaxValue);
        foreach (Vector3 v3 in v)
        {
            if (v3.Z < result.Z)
            {
                result = v3;
            }
        }
        return result;
    }

    public static Vector3[,] GenerateSlopeFieldFromTerrain(Dictionary<Vector2, TerrainPoint> terrain)
    {
        Vector3[,] slopeField = new Vector3[(int) WORLD_SIZE.X, (int) WORLD_SIZE.Y];
        slopeField[0,0] = new Vector3(0, 0, 0);
        for (int x = 0; x < WORLD_SIZE.X - 1; x++)
        {
            for (int y = 0; y < WORLD_SIZE.Y - 1; y++)
            {
                Vector2 i = new Vector2(x, y);
                double z = terrain[i].Position.Z;
                
                Vector2 pRight = i + new Vector2(1, 0);
                Vector2 pUp = i + new Vector2(0, 1);
                Vector2 pRightUp= i + new Vector2(1, 1);
                
                Vector3 vRight = new Vector3(new Vector2(1, 0), (float) z - terrain[pRight].Position.Z);
                Vector3 vUp = new Vector3(new Vector2(0, 1), (float) z - terrain[pUp].Position.Z);
                Vector3 vRightUp = new Vector3(new Vector2(1, 1), (float) z - terrain[pRightUp].Position.Z);

                slopeField[x, y] = MaxZ([slopeField[x, y], vRight, vUp, vRightUp, new Vector3(0, 0, 0)]);
                slopeField[x + 1, y] = MaxZ([slopeField[x + 1, y], -vRight, new Vector3(0, 0, 0)]);
                slopeField[x, y + 1] = MaxZ([slopeField[x, y + 1], -vUp, new Vector3(0, 0, 0)]);
                slopeField[x + 1, y + 1] = MaxZ([slopeField[x + 1, y + 1], -vRightUp, new Vector3(0, 0, 0)]);
            }
        }
        return slopeField;
    }
}