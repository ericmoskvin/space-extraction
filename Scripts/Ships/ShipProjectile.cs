using System.Collections.Generic;
using Godot;
using SpaceExtraction.World;

namespace SpaceExtraction.Ships;

/// <summary>
/// Server-authoritative projectile fired by ship weaponry (Milestones 1 & 2).
/// Performs continuous collision checks against target ships on the server,
/// applying both structural hull damage and targeted subsystem damage.
/// </summary>
public partial class ShipProjectile : Node2D
{
    public int ProjectileId { get; private set; }
    public int SourceShipId { get; private set; }
    public int SourceTeamId { get; private set; }
    public Vector2 Direction { get; private set; } = Vector2.Right;
    public float Speed { get; private set; } = 950.0f;
    public float Damage { get; private set; } = 40.0f;
    public float SubsystemDamage { get; private set; } = 15.0f;
    public ShipSubsystemType TargetSubsystem { get; private set; } = ShipSubsystemType.Engines;
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
        Color boltColor,
        float subsystemDamage = 15.0f,
        ShipSubsystemType targetSubsystem = ShipSubsystemType.Engines)
    {
        Name = $"Projectile_{projectileId}";
        _gameWorld = gameWorld;
        ProjectileId = projectileId;
        SourceShipId = sourceShipId;
        SourceTeamId = sourceTeamId;
        Direction = direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector2.Right;
        Speed = speed;
        Damage = damage;
        SubsystemDamage = subsystemDamage;
        TargetSubsystem = targetSubsystem;
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

                if (ship.TryIntersectProjectileSegment(prevPos, nextPos, out Vector2 worldEntryPoint))
                {
                    ship.ApplyPositionalCombatHitServer(worldEntryPoint, Damage, SubsystemDamage, SourceShipId);
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
        // Bright energy bolt along local X axis forward from muzzle tip
        DrawLine(new Vector2(0.0f, 0.0f), new Vector2(18.0f, 0.0f), BoltColor, 3.0f);
        DrawCircle(new Vector2(16.0f, 0.0f), 2.5f, Colors.White);
    }
}
