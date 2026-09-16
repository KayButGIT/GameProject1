using System;
using UnityEngine;

public static class GridMovementRegression
{
    private const float Radius = 0.36f;
    private static int passed;

    public static int Main()
    {
        try
        {
            Run("Open movement preserves speed", OpenMovement);
            Run("Wall blocking uses the full collider radius", ActualColliderRadius);
            Run("Holding against a wall stays still", WallContact);
            Run("Diagonal input slides along a wall", WallSlide);
            Run("Offset corners curve into the passage in all directions", () => Corners(Radius));
            Run("Full-size collider curves around corners without overlap", () => Corners(0.5f));
            Run("Concave corner stops and allows reversal", ConcaveCorner);
            Run("Diagonal wall gaps stay blocked", DiagonalGap);
            Run("Long movement cannot tunnel", LongMovement);
            Run("Existing corner overlap recovers", OverlapRecovery);
            Run("Bomb exit and re-entry blocking", BombExit);
            Run("Small and large inspector radii remain usable", ColliderRadii);
            Run("Oversized collider cannot enter a narrow passage", NarrowPassage);
            Console.WriteLine($"PASS: {passed} movement regression groups");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Run(string name, Action test)
    {
        test();
        passed++;
        Console.WriteLine($"PASS: {name}");
    }

    private static void OpenMovement()
    {
        Vector2 start = new(2.1f, -4.2f);
        Vector2 delta = new(0.09f, 0.07f);
        Vector2 end = GridMovement.Move(start, delta, Radius, _ => false);
        Require(Vector2.Distance(end, start + delta) < 0.00001f, "Open movement lost distance");
    }

    private static void ActualColliderRadius()
    {
        Func<Vector2Int, bool> wall = cell => cell.x == 1;
        foreach (float radius in new[] { 0.2f, 0.36f, 0.5f, 0.55f })
        {
            Vector2 position = new(-1f, 0f);
            for (int i = 0; i < 50; i++)
            {
                position = Advance(position, new Vector2(0.12f, 0f), wall, radius);
            }
            Require(Mathf.Abs(position.x - (0.54f - radius - 0.001f)) < 0.00001f,
                $"Blocking radius differs from collider radius {radius}");
            Vector2 contact = position;
            for (int i = 0; i < 100; i++)
            {
                position = Advance(position, new Vector2(0.12f, 0f), wall, radius);
                Require(Vector2.Distance(contact, position) < 0.00001f, "Full-radius contact jitter");
            }
        }
    }

    private static void WallContact()
    {
        Func<Vector2Int, bool> wall = cell => cell.x == 1;
        Vector2 position = Vector2.zero;
        for (int i = 0; i < 30; i++)
        {
            position = Advance(position, new Vector2(0.12f, 0f), wall);
        }

        Vector2 contact = position;
        for (int i = 0; i < 500; i++)
        {
            position = Advance(position, new Vector2(0.12f, 0f), wall);
            Require(Vector2.Distance(position, contact) < 0.00001f, "Wall contact jitter");
        }

        Require(position.x > 0.17f && position.x < 0.181f, "Wrong wall clearance");
        Require(Mathf.Abs(position.y) < 0.00001f, "Drift along a closed wall");
    }

    private static void WallSlide()
    {
        Func<Vector2Int, bool> wall = cell => cell.x == 1;
        Vector2 position = new(0.179f, -2f);
        for (int i = 0; i < 100; i++)
        {
            Vector2 next = Advance(position, new Vector2(0.08f, 0.08f), wall);
            Require(next.y - position.y > 0.079f, "Wall tangential motion blocked");
            position = next;
        }
    }

    private static void Corners(float radius)
    {
        foreach (float dt in new[] { 0.01f, 0.02f, 0.0333333f, 0.05f })
        {
            foreach (float offset in new[] { -0.15f, 0f, 0.2f, 0.35f, 0.49f, 0.51f, 0.55f })
            {
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    for (int mirror = -1; mirror <= 1; mirror += 2)
                    {
                        int turn = rotation;
                        int flip = mirror;
                        Func<Vector2Int, bool> walls = cell =>
                        {
                            Vector2 local = Rotate((Vector2)cell, -turn);
                            local.x *= flip;
                            return local.x <= -1f || local.y <= -1f || (local.x == 1f && local.y == 1f);
                        };
                        Vector2 position = Rotate(new Vector2(offset * flip, 0f), turn);
                        position = GridMovement.Move(position, Vector2.zero, radius, walls);
                        AssertClear(position, radius, walls);
                        Vector2 heading = Rotate(Vector2.up, turn);
                        int stalled = 0;
                        for (int frame = 0; frame < Mathf.CeilToInt(0.7f / dt); frame++)
                        {
                            Vector2 next = Advance(position, heading * (6f * dt), walls, radius);
                            Require(Vector2.Dot(next - position, heading) >= -0.00001f, "Corner pushed backward");
                            if (Vector2.Distance(next, position) < 0.0001f)
                            {
                                stalled++;
                            }
                            position = next;
                        }

                        Require(Vector2.Dot(position, heading) > 2.8f,
                            $"Corner stuck: dt={dt}, offset={offset}, rotation={turn}, mirror={flip}, x={position.x}, y={position.y}");
                        Require(stalled == 0, "Corner had stationary frames");
                    }
                }
            }
        }
    }

    private static void ConcaveCorner()
    {
        Func<Vector2Int, bool> walls = cell => cell.x == 1 || cell.y == 1;
        Vector2 position = Vector2.zero;
        for (int i = 0; i < 200; i++)
        {
            position = Advance(position, new Vector2(0.08f, 0.08f), walls);
        }

        Require(position.x < 0.181f && position.y < 0.181f, "Crossed concave corner");
        Vector2 reversed = Advance(position, new Vector2(-0.08f, -0.08f), walls);
        Require(reversed.x < position.x - 0.079f && reversed.y < position.y - 0.079f, "Cannot reverse out of corner");
    }

    private static void DiagonalGap()
    {
        Func<Vector2Int, bool> walls = cell => cell == new Vector2Int(0, 1) || cell == new Vector2Int(1, 0);
        Vector2 position = Vector2.zero;
        for (int i = 0; i < 200; i++)
        {
            position = Advance(position, new Vector2(0.08f, 0.08f), walls);
            Require(position.x < 0.5f && position.y < 0.5f, "Squeezed between diagonal walls");
        }
    }

    private static void LongMovement()
    {
        Func<Vector2Int, bool> wall = cell => cell.x == 1;
        Vector2 position = Advance(Vector2.zero, new Vector2(20f, 0f), wall);
        Require(position.x < 0.181f, "Tunneled through wall");
    }

    private static void OverlapRecovery()
    {
        Func<Vector2Int, bool> wall = cell => cell == Vector2Int.one;
        Vector2 position = GridMovement.Move(new Vector2(0.49f, 0.49f), Vector2.zero, Radius, wall);
        AssertClear(position, Radius, wall);
        for (int i = 0; i < 40; i++)
        {
            position = Advance(position, new Vector2(0f, 0.12f), wall);
        }
        Require(position.y > 3f, "Recovered overlap stayed stuck");
    }

    private static void BombExit()
    {
        bool bombBlocks = false;
        Func<Vector2Int, bool> blocked = cell => Mathf.Abs(cell.y) >= 1 || (bombBlocks && cell == Vector2Int.zero);
        Vector2 position = Vector2.zero;
        for (int i = 0; i < 20; i++)
        {
            position = Advance(position, new Vector2(0.12f, 0f), blocked);
            bombBlocks |= position.x >= 1.2f;
        }
        Require(position.x > 2.3f, "Could not leave newly placed bomb");
        for (int i = 0; i < 40; i++)
        {
            position = Advance(position, new Vector2(-0.12f, 0f), blocked);
        }
        Require(position.x >= 0.82f && position.x < 0.83f, "Bomb did not block re-entry");
        Vector2 escaped = Advance(position, new Vector2(0.12f, 0f), blocked);
        Require(escaped.x > position.x + 0.119f, "Stuck beside blocking bomb");
    }

    private static void ColliderRadii()
    {
        Func<Vector2Int, bool> walls = cell => Mathf.Abs(cell.x) >= 1;
        foreach (float radius in new[] { 0.05f, 0.2f, 0.36f, 0.5f })
        {
            Vector2 position = Vector2.zero;
            for (int i = 0; i < 100; i++)
            {
                position = GridMovement.Move(position, new Vector2(0f, 0.12f), radius, walls);
                AssertClear(position, radius, walls);
            }
            Require(position.y > 11.9f, $"Radius {radius} blocked a one-cell corridor");
        }
    }

    private static void NarrowPassage()
    {
        Func<Vector2Int, bool> walls = cell => Mathf.Abs(cell.x) >= 1 && cell.y >= 0;
        Vector2 position = new(0f, -2f);
        for (int i = 0; i < 100; i++)
        {
            position = Advance(position, new Vector2(0f, 0.12f), walls, 0.55f);
        }
        Require(position.y < -0.46f, "Oversized collider was silently shrunk to enter passage");
    }

    private static Vector2 Advance(Vector2 position, Vector2 delta, Func<Vector2Int, bool> blocked, float radius = Radius)
    {
        Vector2 next = GridMovement.Move(position, delta, radius, blocked);
        Require(Vector2.Distance(next, position) <= delta.magnitude + 0.0001f, "Movement snapped or gained speed");
        AssertClear(next, radius, blocked);
        return next;
    }

    private static void AssertClear(Vector2 position, float radius, Func<Vector2Int, bool> blocked)
    {
        int cellX = Mathf.RoundToInt(position.x);
        int cellY = Mathf.RoundToInt(position.y);
        for (int x = cellX - 2; x <= cellX + 2; x++)
        {
            for (int y = cellY - 2; y <= cellY + 2; y++)
            {
                if (!blocked(new Vector2Int(x, y)))
                {
                    continue;
                }
                float dx = Mathf.Max(0f, Mathf.Abs(position.x - x) - 0.46f);
                float dy = Mathf.Max(0f, Mathf.Abs(position.y - y) - 0.46f);
                Require(dx * dx + dy * dy >= radius * radius - 0.00002f,
                    $"Overlaps wall {x},{y} at {position.x},{position.y}");
            }
        }
    }

    private static Vector2 Rotate(Vector2 value, int turns)
    {
        turns = (turns % 4 + 4) % 4;
        for (int i = 0; i < turns; i++)
        {
            value = new Vector2(-value.y, value.x);
        }
        return value;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
