using System.Collections.Generic;
using Godot;
using SpaceExtraction.World;

namespace SpaceExtraction.Ships;

/// <summary>
/// Server-authoritative projectile fired by ship weaponry (Milestone 1).
/// Performs continuous collision checks against target ships on the server.
/// </summary>
public partial class ShipProjectile : Node2D
{
    public int ProjectileId { get; private set; }
    public int SourceShipId { get; private set; }
    public int SourceTeamId { get; private set; }
    public Vector2 Direction { get; private set; } = Vector2.Right;
    public float Speed { get; private set; } = 950.0f;
    public float Damage { get; private set; } = 40.0f;
    public float RemainingLifetime { get; private set; } = 1.8f;
    public Color BoltColor { get; private set; } = new Color(1.0f, 0.85f, 0.25f);

    private GameWorld? _gameWorld;

    public void Initialize(
        GameWorld gameWorld,
        int projectileId,
        int sourceShipId,
        int sourceTeamId,
        Vector2 startPosition,
        Vector2 direction,
        float speed,
        float damage,
        float lifetime,
        Color boltColor)
    {
        Name = $"Projectile_{projectileId}";
        _gameWorld = gameWorld;
        ProjectileId = projectileId;
        SourceShipId = sourceShipId;
        SourceTeamId = sourceTeamId;
        Direction = direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector2.Right;
        Speed = speed;
        Damage = damage;
        RemainingLifetime = lifetime;
        BoltColor = boltColor;

        Position = startPosition;
        Rotation = Direction.Angle();
        QueueRedraw();
    }

    public override void _Ready()
    {
        if (_gameWorld == null && GetParent()?.GetParent() is GameWorld world)
        {
            _gameWorld = world;
        }

        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Vector2 prevPos = GlobalPosition;
        Vector2 nextPos = prevPos + Direction * (Speed * dt);
        GlobalPosition = nextPos;
        RemainingLifetime -= dt;

        if (Multiplayer.HasMultiplayerPeer() &&
            Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer &&
            Multiplayer.IsServer() &&
            _gameWorld != null)
        {
            int shipCount = _gameWorld.GetShipCount();
            for (int i = 0; i < shipCount; i++)
            {
                Ship? ship = _gameWorld.GetShipAtIndex(i);
                if (ship == null || ship.ShipId == SourceShipId)
                {
                    continue;
                }

                Vector2 closest = Geometry2D.GetClosestPointToSegment(ship.GlobalPosition, prevPos, nextPos);
                float collisionRadius = (ship.MovementProfile?.CollisionRadius ?? 28.0f) + 4.0f;
                if (closest.DistanceTo(ship.GlobalPosition) <= collisionRadius)
                {
                    ship.ApplyHullDamageServer(Damage, SourceShipId);
                    _gameWorld.DespawnProjectileServer(ProjectileId);
                    return;
                }
            }
        }

        if (RemainingLifetime <= 0.0f)
        {
            QueueFree();
        }
    }

    public override void _Draw()
    {
        // Bright energy bolt along local X axis
        DrawLine(new Vector2(-14.0f, 0.0f), new Vector2(6.0f, 0.0f), BoltColor, 3.0f);
        DrawCircle(new Vector2(4.0f, 0.0f), 2.5f, Colors.White);
    }
}
