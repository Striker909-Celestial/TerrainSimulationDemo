using System.Numerics;

namespace TerrainGenerator;

public class TerrainPoint(Vector3 position3D, double baseAbsorptionRate, double baseVegetationGrowthRate, double baseErosionRate)
{
    public Vector3 Position { get; private set; } = position3D;
    public double Temperature { get; private set; } = WorldConfig.TemperatureGradient(position3D);
    public double Pressure { get; private set; } = WorldConfig.PressureGradient(position3D.Z);
    
    public double Groundwater { get; set; } = 0.0;
    public double Runoff { get; private set; } = 0.0;
    public double Vegetation { get; private set; } = 0.0;
    
    public double Age { get; private set; } = 0.0;
    public double TotalPrecipitation { get; private set; } = 0.0;

    private double _baseAbsorptionRate = baseAbsorptionRate;
    private double _absorptionRate = baseAbsorptionRate;
    private double _evaporationRate = 0;
    private double _baseVegetationGrowthRate = baseVegetationGrowthRate;
    private double _vegetationGrowthRate = 0;
    private double _baseErosionRate = baseErosionRate;
    private double _erosionRate = 0;

    private void CalculateAbsorptionRate()
    {
        _absorptionRate = Math.Pow(_baseAbsorptionRate, Groundwater);
    }

    private void CalculateEvaporationRate()
    {
        double antoine = Math.Pow(10, 8.14019 - (1810.94 / (244.485 + Temperature)));
        _evaporationRate = (Pressure - antoine) * Math.Sqrt(3.44586e-5 / Temperature);
        if (double.IsNaN(_evaporationRate))
        {
            _evaporationRate = 0;
        }
    }

    private void CalculateVegetationGrowthRate()
    {
        _vegetationGrowthRate = Math.Pow(_baseVegetationGrowthRate, Vegetation) * Groundwater;
        if (double.IsNaN(_vegetationGrowthRate))
        {
            _vegetationGrowthRate = 0;
        }
    }
    
    private void CalculateErosionRate(double slope)
    {
        _erosionRate = Math.Pow(_baseErosionRate, Math.Max(Vegetation - Runoff, 1));
        if (double.IsNaN(_erosionRate))
        {
            _erosionRate = 0;
        }
    }

    public Dictionary<string, double> Tick(double slope, double precipitation, double runoff, double sediment, Random random)
    {
        //deposit precipitation and runoff
        TotalPrecipitation += precipitation;
        Runoff += runoff + precipitation;
        Age++;
        
        //update Temperature and Pressure
        Temperature = WorldConfig.TemperatureGradient(Position);
        Pressure = WorldConfig.PressureGradient(Position.Z);
        
        //calculate rates
        CalculateAbsorptionRate();
        CalculateEvaporationRate();
        CalculateVegetationGrowthRate();
        CalculateErosionRate(slope);
        
        //absorb Runoff
        var amountAbsorbed = Math.Min(0.9 * _absorptionRate + 0.1 * random.NextDouble(), Runoff / 2);
        Runoff -= amountAbsorbed;
        Groundwater += amountAbsorbed;
        
        //evaporate Runoff
        var amountEvaporated = Math.Min(0.9 * _evaporationRate + 0.1 * random.NextDouble(), Runoff);
        Runoff -= amountEvaporated;
        
        //grow Vegetation
        var groundwaterConsumed = Math.Min(Groundwater, Vegetation);
        Groundwater -= groundwaterConsumed;
        amountEvaporated += Math.Max(0, Vegetation - groundwaterConsumed);
        var amountGrown = Math.Min(0.9 * _vegetationGrowthRate + 0.1 * random.NextDouble(), Groundwater);
        Vegetation = (Groundwater == 0) ? groundwaterConsumed : Vegetation + amountGrown;
        
        //erode sediment
        var amountEroded = Math.Min(0.9 * _erosionRate + 0.1 * random.NextDouble(), Position.Z);
        Position = Position with { Z = Position.Z - (float) amountEroded + (float) sediment };
        if (Position.Z > WorldConfig.MAX_HEIGHT)
        {
            amountEroded += Position.Z - WorldConfig.MAX_HEIGHT;
            Position = Position with { Z = (float)WorldConfig.MAX_HEIGHT };
        }
        
        //outputs Runoff, sediment, and vapor produced
        Dictionary<string, double> result = new Dictionary<String, double>();
        result.Add("runoff", Runoff * Math.Max(1.0, slope));
        Runoff -= Runoff * Math.Max(1.0, slope);
        result.Add("sediment", amountEroded);
        result.Add("vapor", amountEvaporated);
        return result;
    }

}