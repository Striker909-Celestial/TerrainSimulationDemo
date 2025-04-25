using System.Numerics;

namespace TerrainGenerator;

public class Cloud(Vector3 position3D, Vector3 initialVelocity, double mass, double volume)
{
    public Vector3 Position { get; private set; } = position3D;
    public Vector3 Velocity { get; private set; } = initialVelocity;
    private Vector3 _acceleration = new Vector3(0,0,0);
    
    public double Mass { get; private set; } = mass;
    public double Volume { get; private set; } = volume;
    
    private double _precipitation = 0.0;

    private void CalculateAcceleration(Vector3 force)
    {
        _acceleration.X = (float) (force.X / Mass);
        _acceleration.Y = (float) (force.Y / Mass);
        _acceleration.Z = (float) (force.Z / Mass);
    }

    private void CalculateVolume()
    {
        double temp = WorldConfig.TemperatureGradient(Position);
        double pressure = WorldConfig.PressureGradient(Position.Z);
        double moles = Mass * 1000 / 18.02;
        Volume = moles * 0.08206 * (temp + 273.15) / pressure;
        if (double.IsNaN(Volume))
        {
            Volume = 0;
        }
    }

    private void CalculatePrecipitation()
    {
        double density = Mass / Volume;
        if (double.IsNaN(density))
        {
            density = 0;
        }
        _precipitation = Math.Min(Math.Pow(10, 4 * density - 3) * Mass, 0.2 * Mass);
        if (double.IsNaN(_precipitation))
        {
            _precipitation = 0;
        }
    }

    private Vector2 FindNearestTile(Vector3 position)
    {
        Vector2 nearestTile = new Vector2(
            (float) Math.Round(position.X), 
            (float) Math.Round(position.Y));
        if (nearestTile.X < 0 || nearestTile.Y < 0 || position.Z < 0 ||
            nearestTile.X >= WorldConfig.WORLD_SIZE.X || nearestTile.Y >= WorldConfig.WORLD_SIZE.Y
            || float.IsNaN(nearestTile.X) || float.IsNaN(nearestTile.Y))
        {
            nearestTile = new Vector2(-1, -1);
        }
        return nearestTile;
    }

    public Dictionary<string, double> Tick(Vector3 windForce, Dictionary<Vector2, TerrainPoint> terrain)
    {
        //calculate Volume, precipitation, and acceleration
        CalculateAcceleration(windForce);
        CalculateVolume();
        CalculatePrecipitation();
        
        //update Velocity by acceleration
        Velocity = Velocity with
        {
            X = Velocity.X + _acceleration.X,
            Y = Velocity.Y + _acceleration.Y,
            Z = Velocity.Z + _acceleration.Z
        };
        
        //update Position by Velocity, taking into account physical barriers
        Vector3 tempPosition = new Vector3(
            Position.X + Velocity.X,
            Position.Y + Velocity.Y,
            Position.Z + Velocity.Z);
        
        Vector2 nearestTile = FindNearestTile(tempPosition);
        if (Math.Abs(nearestTile.X - (-1.0)) < 0.99 || terrain[nearestTile].Position.Z > tempPosition.Z)
        {
            tempPosition = Position;
        }
        Position = tempPosition;

        //subtract precipitation from mass, then output precipitation, target_x, and target_y
        Mass -= _precipitation;
        Vector2 target = FindNearestTile(Position);
        Dictionary<string, double> result = new Dictionary<string, double>();
        result.Add("precipitation", _precipitation);
        result.Add("target_x", target.X);
        result.Add("target_y", target.Y);

        return result;
    }

    public static Cloud operator +(Cloud cloud1, Cloud cloud2)
    {
        return new Cloud(
            (cloud1.Position + cloud2.Position) / 2, 
            (cloud1.Velocity + cloud2.Velocity) / 2, 
            cloud1.Mass + cloud2.Mass, 
            cloud1.Volume + cloud2.Volume);
    }
}