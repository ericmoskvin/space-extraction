using System.Collections.Generic;
using Godot;
using SpaceExtraction.Characters;

namespace SpaceExtraction.World;

/// <summary>
/// Root world scene container for Milestone 0.
/// Manages spatial partitioning containers for space environment, ships, and players.
/// Renders an observable background grid for visual movement tracking.
/// </summary>
public partial class GameWorld : Node2D
{
    private Node2D? _spaceEnvironment;
    private Node2D? _shipsContainer;
    private Node2D? _playersContainer;

    public Node2D SpaceEnvironment
    {
        get
        {
            EnsureContainers();
            return _spaceEnvironment!;
        }
        private set => _spaceEnvironment = value;
    }

    public Node2D ShipsContainer
    {
        get
        {
            EnsureContainers();
            return _shipsContainer!;
        }
        private set => _shipsContainer = value;
    }

    public Node2D PlayersContainer
    {
        get
        {
            EnsureContainers();
            return _playersContainer!;
        }
        private set => _playersContainer = value;
    }

    public Rect2 WorldBounds { get; set; } = new Rect2(-2000f, -2000f, 4000f, 4000f);

    public override void _EnterTree()
    {
        EnsureContainers();
    }

    public override void _Ready()
    {
        EnsureContainers();
        QueueRedraw();
    }

    /// <summary>
    /// Ensures child containers exist and are parented, even if accessed before _Ready().
    /// </summary>
    public void EnsureContainers()
    {
        if (_spaceEnvironment == null || !IsInstanceValid(_spaceEnvironment))
        {
            _spaceEnvironment = GetNodeOrNull<Node2D>("SpaceEnvironment") ?? new Node2D { Name = "SpaceEnvironment" };
            if (_spaceEnvironment.GetParent() == null)
            {
                AddChild(_spaceEnvironment);
            }
        }

        if (_shipsContainer == null || !IsInstanceValid(_shipsContainer))
        {
            _shipsContainer = GetNodeOrNull<Node2D>("ShipsContainer") ?? new Node2D { Name = "ShipsContainer" };
            if (_shipsContainer.GetParent() == null)
            {
                AddChild(_shipsContainer);
            }
        }

        if (_playersContainer == null || !IsInstanceValid(_playersContainer))
        {
            _playersContainer = GetNodeOrNull<Node2D>("PlayersContainer") ?? new Node2D { Name = "PlayersContainer" };
            if (_playersContainer.GetParent() == null)
            {
                AddChild(_playersContainer);
            }
        }
    }

    public override void _Draw()
    {
        // Dark space background
        DrawRect(WorldBounds, new Color(0.03f, 0.04f, 0.07f, 1.0f), filled: true);

        // Subtle grid lines every 128 units
        var gridColor = new Color(0.12f, 0.16f, 0.22f, 0.6f);
        const float step = 128.0f;

        float startX = Mathf.Floor(WorldBounds.Position.X / step) * step;
        float endX = WorldBounds.End.X;
        for (float x = startX; x <= endX; x += step)
        {
            DrawLine(new Vector2(x, WorldBounds.Position.Y), new Vector2(x, WorldBounds.End.Y), gridColor, 1.0f);
        }

        float startY = Mathf.Floor(WorldBounds.Position.Y / step) * step;
        float endY = WorldBounds.End.Y;
        for (float y = startY; y <= endY; y += step)
        {
            DrawLine(new Vector2(WorldBounds.Position.X, y), new Vector2(WorldBounds.End.X, y), gridColor, 1.0f);
        }

        // Brighter center axes (X=0 and Y=0)
        var axisColor = new Color(0.25f, 0.45f, 0.65f, 0.8f);
        if (WorldBounds.Position.X <= 0f && WorldBounds.End.X >= 0f)
        {
            DrawLine(new Vector2(0f, WorldBounds.Position.Y), new Vector2(0f, WorldBounds.End.Y), axisColor, 2.0f);
        }
        if (WorldBounds.Position.Y <= 0f && WorldBounds.End.Y >= 0f)
        {
            DrawLine(new Vector2(WorldBounds.Position.X, 0f), new Vector2(WorldBounds.End.X, 0f), axisColor, 2.0f);
        }

        // Outer boundary border
        DrawRect(WorldBounds, new Color(0.35f, 0.55f, 0.85f, 0.8f), filled: false, width: 2.0f);
    }

    public PlayerCharacter? GetPlayerCharacter(long peerId)
    {
        EnsureContainers();
        int count = PlayersContainer.GetChildCount();
        for (int i = 0; i < count; i++)
        {
            if (PlayersContainer.GetChild(i) is PlayerCharacter pc && pc.PeerId == peerId)
            {
                return pc;
            }
        }
        return null;
    }

    public IReadOnlyList<PlayerCharacter> GetAllPlayerCharacters()
    {
        EnsureContainers();
        int count = PlayersContainer.GetChildCount();
        var list = new List<PlayerCharacter>(count);
        for (int i = 0; i < count; i++)
        {
            if (PlayersContainer.GetChild(i) is PlayerCharacter pc)
            {
                list.Add(pc);
            }
        }
        return list;
    }
}
