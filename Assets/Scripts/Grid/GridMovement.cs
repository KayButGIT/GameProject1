using System;
using UnityEngine;

public static class GridMovement
{
    // Matches the visible blocks and their BoxColliders in BombermanMap.CreateBlock.
    private const float WallHalfSize = 0.46f;
    private const float Skin = 0.001f;
    private const float CornerAssistReach = 0.6f;

    public static Vector2 Move(Vector2 position, Vector2 delta, float radius, Func<Vector2Int, bool> isBlocked)
    {
        radius = Mathf.Max(radius, 0.0001f) + Skin;
        position = ResolveContacts(position, radius, isBlocked);
        float distance = delta.magnitude;
        if (distance < 0.000001f)
        {
            return position;
        }

        // Small steps prevent tunneling and follow the changing normal around each rounded corner.
        int steps = Mathf.CeilToInt(distance / Mathf.Min(0.025f, radius * 0.25f));
        Vector2 step = delta / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector2 candidate = ResolveContacts(position + step, radius, isBlocked);
            candidate = AssistCorner(position, step, candidate, radius, isBlocked);
            if (IsClear(candidate, radius, isBlocked))
            {
                position = candidate;
            }
        }

        return position;
    }

    private static Vector2 AssistCorner(
        Vector2 position, Vector2 step, Vector2 candidate, float radius, Func<Vector2Int, bool> isBlocked)
    {
        bool horizontal = Mathf.Abs(step.x) > Mathf.Abs(step.y);
        float forward = horizontal ? step.x : step.y;
        float sideways = horizontal ? step.y : step.x;
        if (Mathf.Abs(sideways) > Mathf.Abs(forward) * 0.2f)
        {
            return candidate;
        }

        Vector2 heading = step.normalized;
        float progress = Mathf.Clamp01(Vector2.Dot(candidate - position, heading) / step.magnitude);
        if (progress > 0.99f)
        {
            return candidate;
        }

        Vector2Int cell = Vector2Int.RoundToInt(position);
        Vector2Int ahead = horizontal
            ? new Vector2Int(forward > 0f ? 1 : -1, 0)
            : new Vector2Int(0, forward > 0f ? 1 : -1);
        float nearestOffset = CornerAssistReach;
        float laneOffset = 0f;
        for (int offset = -1; offset <= 1; offset++)
        {
            Vector2Int lane = cell + (horizontal ? new Vector2Int(0, offset) : new Vector2Int(offset, 0));
            float correction = horizontal ? lane.y - position.y : lane.x - position.x;
            if (Mathf.Abs(correction) >= nearestOffset || isBlocked(lane) || isBlocked(lane + ahead))
            {
                continue;
            }

            nearestOffset = Mathf.Abs(correction);
            laneOffset = correction;
        }

        if (Mathf.Abs(laneOffset) < Skin)
        {
            return candidate;
        }

        float assist = Mathf.Clamp(laneOffset / radius, -1f, 1f) * (1f - progress);
        Vector2 bend = horizontal ? new Vector2(0f, assist) : new Vector2(assist, 0f);
        Vector2 assistedStep = (heading + bend).normalized * step.magnitude;
        // Never cross the lane center, even at low frame rates.
        if (horizontal)
        {
            assistedStep.y = Mathf.Clamp(assistedStep.y, -Mathf.Abs(laneOffset), Mathf.Abs(laneOffset));
        }
        else
        {
            assistedStep.x = Mathf.Clamp(assistedStep.x, -Mathf.Abs(laneOffset), Mathf.Abs(laneOffset));
        }

        Vector2 assisted = ResolveContacts(position + assistedStep, radius, isBlocked);
        return IsClear(assisted, radius, isBlocked) ? assisted : candidate;
    }

    private static Vector2 ResolveContacts(Vector2 position, float radius, Func<Vector2Int, bool> isBlocked)
    {
        for (int iteration = 0; iteration < 8; iteration++)
        {
            bool corrected = false;
            Vector2Int center = Vector2Int.RoundToInt(position);
            int reach = Mathf.CeilToInt(radius + WallHalfSize);
            for (int x = center.x - reach; x <= center.x + reach; x++)
            {
                for (int y = center.y - reach; y <= center.y + reach; y++)
                {
                    Vector2Int cell = new(x, y);
                    if (!isBlocked(cell))
                    {
                        continue;
                    }

                    Vector2 closest = ClosestPoint(position, cell);
                    Vector2 separation = position - closest;
                    float distanceSquared = separation.sqrMagnitude;
                    if (distanceSquared >= radius * radius)
                    {
                        continue;
                    }

                    if (distanceSquared > 0.00000001f)
                    {
                        position = closest + separation * (radius / Mathf.Sqrt(distanceSquared));
                    }
                    else
                    {
                        // Recover an existing overlap, including a newly blocking bomb.
                        Vector2 offset = position - (Vector2)cell;
                        if (Mathf.Abs(offset.x) > Mathf.Abs(offset.y))
                        {
                            position.x = cell.x + (offset.x >= 0f ? 1f : -1f) * (WallHalfSize + radius);
                        }
                        else
                        {
                            position.y = cell.y + (offset.y >= 0f ? 1f : -1f) * (WallHalfSize + radius);
                        }
                    }

                    corrected = true;
                }
            }

            if (!corrected)
            {
                break;
            }
        }

        return position;
    }

    private static bool IsClear(Vector2 position, float radius, Func<Vector2Int, bool> isBlocked)
    {
        Vector2Int center = Vector2Int.RoundToInt(position);
        float minimumDistance = radius - 0.00001f;
        int reach = Mathf.CeilToInt(radius + WallHalfSize);
        for (int x = center.x - reach; x <= center.x + reach; x++)
        {
            for (int y = center.y - reach; y <= center.y + reach; y++)
            {
                Vector2Int cell = new(x, y);
                if (isBlocked(cell) && (position - ClosestPoint(position, cell)).sqrMagnitude < minimumDistance * minimumDistance)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static Vector2 ClosestPoint(Vector2 position, Vector2Int cell)
    {
        return new Vector2(
            Mathf.Clamp(position.x, cell.x - WallHalfSize, cell.x + WallHalfSize),
            Mathf.Clamp(position.y, cell.y - WallHalfSize, cell.y + WallHalfSize));
    }
}
