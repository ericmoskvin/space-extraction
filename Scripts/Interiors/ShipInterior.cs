using System;
using System.Collections.Generic;
using Godot;

namespace SpaceExtraction.Interiors;

/// <summary>
/// Physical ship interior simulation model (Section 31 & Milestone 3).
/// Manages room compartments, bulkheads/doors, operational consoles, collision/walkable bounds,
/// and smooth sliding movement resolution within the ship's local coordinate frame.
/// </summary>
public sealed class ShipInterior
{
    public const float CharacterCollisionMargin = 8.0f;

    private readonly List<InteriorRoom> _rooms = new();
    private readonly List<InteriorDoor> _doors = new();
    private readonly List<InteriorConsole> _consoles = new();

    public IReadOnlyList<InteriorRoom> Rooms => _rooms;
    public IReadOnlyList<InteriorDoor> Doors => _doors;
    public IReadOnlyList<InteriorConsole> Consoles => _consoles;

    public InteriorConsoleType LastInteractedConsoleType { get; private set; } = InteriorConsoleType.None;
    public long LastInteractedByPeerId { get; private set; }
    public int ConsoleInteractionCount { get; private set; }

    public void RecordConsoleInteraction(InteriorConsoleType type, long peerId)
    {
        LastInteractedConsoleType = type;
        LastInteractedByPeerId = peerId;
        ConsoleInteractionCount++;
    }

    /// <summary>
    /// Factory that constructs the authored Corvette physical interior layout in ship-local coordinates:
    /// (0, 0) center, +X bow, -X stern, -Y port, +Y starboard.
    /// </summary>
    public static ShipInterior CreateCorvetteInterior()
    {
        var interior = new ShipInterior();

        // 1. Rooms (5)
        interior._rooms.Add(new InteriorRoom
        {
            RoomType = InteriorRoomType.Cockpit,
            RoomName = "Cockpit",
            Bounds = new Rect2(52.0f, -48.0f, 104.0f, 96.0f),
            WalkableRect = new Rect2(52.0f, -48.0f, 104.0f, 96.0f).Grow(-CharacterCollisionMargin),
            FloorColor = new Color(0.12f, 0.24f, 0.34f, 0.94f)
        });

        interior._rooms.Add(new InteriorRoom
        {
            RoomType = InteriorRoomType.CentralHub,
            RoomName = "Central Hub",
            Bounds = new Rect2(-52.0f, -48.0f, 104.0f, 96.0f),
            WalkableRect = new Rect2(-52.0f, -48.0f, 104.0f, 96.0f).Grow(-CharacterCollisionMargin),
            FloorColor = new Color(0.24f, 0.19f, 0.14f, 0.94f)
        });

        interior._rooms.Add(new InteriorRoom
        {
            RoomType = InteriorRoomType.EngineRoom,
            RoomName = "Engine Room",
            Bounds = new Rect2(-156.0f, -56.0f, 104.0f, 112.0f),
            WalkableRect = new Rect2(-156.0f, -56.0f, 104.0f, 112.0f).Grow(-CharacterCollisionMargin),
            FloorColor = new Color(0.28f, 0.15f, 0.13f, 0.94f)
        });

        interior._rooms.Add(new InteriorRoom
        {
            RoomType = InteriorRoomType.Cargo,
            RoomName = "Cargo Bay",
            Bounds = new Rect2(-52.0f, -112.0f, 104.0f, 64.0f),
            WalkableRect = new Rect2(-52.0f, -112.0f, 104.0f, 64.0f).Grow(-CharacterCollisionMargin),
            FloorColor = new Color(0.26f, 0.23f, 0.12f, 0.94f)
        });

        interior._rooms.Add(new InteriorRoom
        {
            RoomType = InteriorRoomType.Medical,
            RoomName = "Medbay",
            Bounds = new Rect2(-52.0f, 48.0f, 104.0f, 64.0f),
            WalkableRect = new Rect2(-52.0f, 48.0f, 104.0f, 64.0f).Grow(-CharacterCollisionMargin),
            FloorColor = new Color(0.12f, 0.26f, 0.20f, 0.94f)
        });

        // 2. Doors (4), defaulting to IsOpen = true
        interior._doors.Add(new InteriorDoor
        {
            DoorId = 0,
            DoorName = "Bridge Door",
            RoomA = InteriorRoomType.CentralHub,
            RoomB = InteriorRoomType.Cockpit,
            LocalPosition = new Vector2(52.0f, 0.0f),
            IsVerticalBulkhead = true,
            VisualRect = new Rect2(48.0f, -20.0f, 8.0f, 40.0f),
            WalkableConnectorRect = new Rect2(42.0f, -16.0f, 20.0f, 32.0f),
            InteractionRadius = 44.0f,
            IsOpen = true
        });

        interior._doors.Add(new InteriorDoor
        {
            DoorId = 1,
            DoorName = "Engine Door",
            RoomA = InteriorRoomType.EngineRoom,
            RoomB = InteriorRoomType.CentralHub,
            LocalPosition = new Vector2(-52.0f, 0.0f),
            IsVerticalBulkhead = true,
            VisualRect = new Rect2(-56.0f, -20.0f, 8.0f, 40.0f),
            WalkableConnectorRect = new Rect2(-62.0f, -16.0f, 20.0f, 32.0f),
            InteractionRadius = 44.0f,
            IsOpen = true
        });

        interior._doors.Add(new InteriorDoor
        {
            DoorId = 2,
            DoorName = "Cargo Door",
            RoomA = InteriorRoomType.CentralHub,
            RoomB = InteriorRoomType.Cargo,
            LocalPosition = new Vector2(0.0f, -48.0f),
            IsVerticalBulkhead = false,
            VisualRect = new Rect2(-20.0f, -52.0f, 40.0f, 8.0f),
            WalkableConnectorRect = new Rect2(-16.0f, -58.0f, 32.0f, 20.0f),
            InteractionRadius = 44.0f,
            IsOpen = true
        });

        interior._doors.Add(new InteriorDoor
        {
            DoorId = 3,
            DoorName = "Medbay Door",
            RoomA = InteriorRoomType.CentralHub,
            RoomB = InteriorRoomType.Medical,
            LocalPosition = new Vector2(0.0f, 48.0f),
            IsVerticalBulkhead = false,
            VisualRect = new Rect2(-20.0f, 44.0f, 40.0f, 8.0f),
            WalkableConnectorRect = new Rect2(-16.0f, 38.0f, 32.0f, 20.0f),
            InteractionRadius = 44.0f,
            IsOpen = true
        });

        // 3. Consoles (5)
        interior._consoles.Add(new InteriorConsole
        {
            ConsoleId = 0,
            ConsoleType = InteriorConsoleType.Pilot,
            ConsoleName = "Pilot Console",
            RoomType = InteriorRoomType.Cockpit,
            LocalPosition = new Vector2(132.0f, 0.0f),
            SeatOrStandLocalPosition = new Vector2(108.0f, 0.0f),
            VisualRect = new Rect2(124.0f, -20.0f, 16.0f, 40.0f),
            ConsoleColor = new Color(0.2f, 0.85f, 1.0f),
            InteractionRadius = 44.0f
        });

        interior._consoles.Add(new InteriorConsole
        {
            ConsoleId = 1,
            ConsoleType = InteriorConsoleType.Turret,
            ConsoleName = "Turret Console",
            RoomType = InteriorRoomType.CentralHub,
            LocalPosition = new Vector2(0.0f, 0.0f),
            SeatOrStandLocalPosition = new Vector2(0.0f, 0.0f),
            VisualRect = new Rect2(-12.0f, -12.0f, 24.0f, 24.0f),
            ConsoleColor = new Color(1.0f, 0.65f, 0.2f),
            InteractionRadius = 44.0f
        });

        interior._consoles.Add(new InteriorConsole
        {
            ConsoleId = 2,
            ConsoleType = InteriorConsoleType.Engineering,
            ConsoleName = "Engineering Console",
            RoomType = InteriorRoomType.EngineRoom,
            LocalPosition = new Vector2(-128.0f, 0.0f),
            SeatOrStandLocalPosition = new Vector2(-104.0f, 0.0f),
            VisualRect = new Rect2(-140.0f, -24.0f, 16.0f, 48.0f),
            ConsoleColor = new Color(1.0f, 0.45f, 0.2f),
            InteractionRadius = 44.0f
        });

        interior._consoles.Add(new InteriorConsole
        {
            ConsoleId = 3,
            ConsoleType = InteriorConsoleType.Cargo,
            ConsoleName = "Cargo Console",
            RoomType = InteriorRoomType.Cargo,
            LocalPosition = new Vector2(0.0f, -92.0f),
            SeatOrStandLocalPosition = new Vector2(0.0f, -76.0f),
            VisualRect = new Rect2(-20.0f, -104.0f, 40.0f, 12.0f),
            ConsoleColor = new Color(0.95f, 0.85f, 0.25f),
            InteractionRadius = 44.0f
        });

        interior._consoles.Add(new InteriorConsole
        {
            ConsoleId = 4,
            ConsoleType = InteriorConsoleType.Medical,
            ConsoleName = "Medical Console",
            RoomType = InteriorRoomType.Medical,
            LocalPosition = new Vector2(0.0f, 92.0f),
            SeatOrStandLocalPosition = new Vector2(0.0f, 76.0f),
            VisualRect = new Rect2(-20.0f, 92.0f, 40.0f, 12.0f),
            ConsoleColor = new Color(0.25f, 0.95f, 0.55f),
            InteractionRadius = 44.0f
        });

        return interior;
    }

    /// <summary>
    /// Returns true if the given ship-local point lies within any room's walkable bounds
    /// or inside any open door's walkable connector rectangle.
    /// </summary>
    public bool IsLocalPointWalkable(Vector2 localPoint)
    {
        for (int i = 0; i < _rooms.Count; i++)
        {
            if (_rooms[i].WalkableRect.HasPoint(localPoint))
            {
                return true;
            }
        }

        for (int i = 0; i < _doors.Count; i++)
        {
            InteriorDoor door = _doors[i];
            if (door.IsOpen && door.WalkableConnectorRect.HasPoint(localPoint))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves local movement from current position along desired delta, sliding smoothly
    /// along room walls and through open bulkhead doorways.
    /// </summary>
    public Vector2 ResolveLocalMovement(Vector2 currentLocalPos, Vector2 desiredDelta)
    {
        if (!IsLocalPointWalkable(currentLocalPos))
        {
            // Snap to the closest room walkable boundary point if currently out of bounds
            float bestDistSq = float.MaxValue;
            Vector2 bestClamped = currentLocalPos;

            for (int i = 0; i < _rooms.Count; i++)
            {
                Rect2 w = _rooms[i].WalkableRect;
                Vector2 clamped = new Vector2(
                    Mathf.Clamp(currentLocalPos.X, w.Position.X, w.End.X),
                    Mathf.Clamp(currentLocalPos.Y, w.Position.Y, w.End.Y));
                float dSq = currentLocalPos.DistanceSquaredTo(clamped);
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestClamped = clamped;
                }
            }

            currentLocalPos = bestClamped;
        }

        if (desiredDelta.LengthSquared() < 0.00001f)
        {
            return currentLocalPos;
        }

        // Subdivide large movement deltas into steps <= 4.0f units so a character can never
        // tunnel across the 10-unit bulkhead gap between rooms when a door is closed.
        float totalDist = desiredDelta.Length();
        int steps = Mathf.Max(1, Mathf.CeilToInt(totalDist / 4.0f));
        Vector2 stepDelta = desiredDelta / steps;

        Vector2 pos = currentLocalPos;
        for (int s = 0; s < steps; s++)
        {
            pos = ResolveSingleStep(pos, stepDelta);
        }
        return pos;
    }

    private Vector2 ResolveSingleStep(Vector2 currentLocalPos, Vector2 stepDelta)
    {
        Vector2 directTarget = currentLocalPos + stepDelta;
        if (IsLocalPointWalkable(directTarget))
        {
            return directTarget;
        }

        // Test two sliding orders (X-then-Y vs Y-then-X) and choose the one achieving greater movement
        Vector2 posA = ResolveAxesOrder(currentLocalPos, stepDelta.X, stepDelta.Y, xFirst: true);
        Vector2 posB = ResolveAxesOrder(currentLocalPos, stepDelta.X, stepDelta.Y, xFirst: false);

        float distSqA = currentLocalPos.DistanceSquaredTo(posA);
        float distSqB = currentLocalPos.DistanceSquaredTo(posB);

        return distSqB > distSqA ? posB : posA;
    }

    private Vector2 ResolveAxesOrder(Vector2 startPos, float dx, float dy, bool xFirst)
    {
        Vector2 resolved = startPos;

        if (xFirst)
        {
            resolved = ResolveSingleAxisX(resolved, dx);
            resolved = ResolveSingleAxisY(resolved, dy);
        }
        else
        {
            resolved = ResolveSingleAxisY(resolved, dy);
            resolved = ResolveSingleAxisX(resolved, dx);
        }

        return resolved;
    }

    private Vector2 ResolveSingleAxisX(Vector2 fromPos, float dx)
    {
        if (Mathf.Abs(dx) < 0.0001f)
        {
            return fromPos;
        }

        Vector2 target = new Vector2(fromPos.X + dx, fromPos.Y);
        if (IsLocalPointWalkable(target))
        {
            return target;
        }

        // Binary search to find maximum walkable distance along X
        float low = 0.0f;
        float high = dx;
        for (int i = 0; i < 8; i++)
        {
            float mid = (low + high) * 0.5f;
            Vector2 test = new Vector2(fromPos.X + mid, fromPos.Y);
            if (IsLocalPointWalkable(test))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return new Vector2(fromPos.X + low, fromPos.Y);
    }

    private Vector2 ResolveSingleAxisY(Vector2 fromPos, float dy)
    {
        if (Mathf.Abs(dy) < 0.0001f)
        {
            return fromPos;
        }

        Vector2 target = new Vector2(fromPos.X, fromPos.Y + dy);
        if (IsLocalPointWalkable(target))
        {
            return target;
        }

        // Binary search to find maximum walkable distance along Y
        float low = 0.0f;
        float high = dy;
        for (int i = 0; i < 8; i++)
        {
            float mid = (low + high) * 0.5f;
            Vector2 test = new Vector2(fromPos.X, fromPos.Y + mid);
            if (IsLocalPointWalkable(test))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return new Vector2(fromPos.X, fromPos.Y + low);
    }

    public InteriorRoom? GetRoomAtLocalPoint(Vector2 localPoint)
    {
        for (int i = 0; i < _rooms.Count; i++)
        {
            if (_rooms[i].ContainsPoint(localPoint))
            {
                return _rooms[i];
            }
        }
        return null;
    }

    public InteriorConsole? GetConsoleByType(InteriorConsoleType type)
    {
        for (int i = 0; i < _consoles.Count; i++)
        {
            if (_consoles[i].ConsoleType == type)
            {
                return _consoles[i];
            }
        }
        return null;
    }

    public InteriorConsole? FindNearestInteractableConsole(Vector2 localPoint)
    {
        InteriorConsole? nearest = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < _consoles.Count; i++)
        {
            InteriorConsole console = _consoles[i];
            float dist = localPoint.DistanceTo(console.LocalPosition);
            if (dist <= console.InteractionRadius && dist < minDistance)
            {
                minDistance = dist;
                nearest = console;
            }
        }

        return nearest;
    }

    public InteriorDoor? GetDoor(int doorId)
    {
        for (int i = 0; i < _doors.Count; i++)
        {
            if (_doors[i].DoorId == doorId)
            {
                return _doors[i];
            }
        }
        return null;
    }

    public InteriorDoor? FindNearestInteractableDoor(Vector2 localPoint)
    {
        InteriorDoor? nearest = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < _doors.Count; i++)
        {
            InteriorDoor door = _doors[i];
            float dist = localPoint.DistanceTo(door.LocalPosition);
            if (dist <= door.InteractionRadius && dist < minDistance)
            {
                minDistance = dist;
                nearest = door;
            }
        }

        return nearest;
    }

    public bool SetDoorOpen(int doorId, bool isOpen)
    {
        InteriorDoor? door = GetDoor(doorId);
        if (door == null)
        {
            return false;
        }

        door.IsOpen = isOpen;
        return true;
    }

    public uint GetDoorsBitmask()
    {
        uint mask = 0;
        for (int i = 0; i < _doors.Count; i++)
        {
            if (_doors[i].IsOpen)
            {
                mask |= (1u << _doors[i].DoorId);
            }
        }
        return mask;
    }

    public void ApplyDoorsBitmask(uint mask)
    {
        for (int i = 0; i < _doors.Count; i++)
        {
            _doors[i].IsOpen = (mask & (1u << _doors[i].DoorId)) != 0;
        }
    }
}
