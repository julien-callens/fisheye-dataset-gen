using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Implements 3D Poisson Disk Sampling to generate a set of points within specified bounds.
/// Each point maintains a minimum distance from others and is only added if it is fully visible
/// (with extra padding) by every active fisheye camera (which share the same distortion shader).
/// When you export these positions, format them with six–decimal precision (e.g. "F6").
/// </summary>
public abstract class PoissonDiskSampling3D
{
    // --- Fisheye Distortion Parameters (assumed same for all cameras) ---
    private const float FisheyeXi = 0.3f;
    private const float FisheyeLambda = 0.3f;
    private const float FisheyeAlpha = 0.4f;

    // --- Ball (object) size information ---
    // We assume a ball diameter of 0.1 m (radius = 0.05 m)
    private const float BallRadius = 0.05f;

    // --- Extra padding in viewport coordinates (0–1 range) ---
    // This ensures that even the outermost parts of the ball are inside the view.
    // For example, a padding of 0.05 means we only accept points whose
    // distorted viewport position falls within [0.05, 0.95].
    private const float ViewportPadding = 0.05f;

    /// <summary>
    /// Generates points using Poisson Disk Sampling within the given bounds.
    /// The points maintain a minimum distance from each other and are visible by every camera.
    /// Adjust the generation parameters (minDist, genBounds) to generate more points (images).
    /// </summary>
    /// <param name="minDist">The minimum required distance between points.</param>
    /// <param name="genBounds">The 3D bounds within which points will be generated.</param>
    /// <param name="maxAttempts">The maximum number of attempts to generate a valid point around an active point.</param>
    /// <returns>A list of generated 3D points.</returns>
    public static List<Vector3> GeneratePoints(float minDist, Vector3 genBounds, int maxAttempts = 30)
    {
        var points = new List<Vector3>();
        var cellSize = minDist / Mathf.Sqrt(3);
        var gridX = Mathf.CeilToInt(genBounds.x / cellSize);
        var gridY = Mathf.CeilToInt(genBounds.y / cellSize);
        var gridZ = Mathf.CeilToInt(genBounds.z / cellSize);

        var grid = new Vector3[gridX, gridY, gridZ];
        var activeList = new List<Vector3>();

        var initialPoint = Vector3.zero;
        var initialFound = false;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            initialPoint = new Vector3(
                Random.Range(-genBounds.x / 2, genBounds.x / 2),
                Random.Range(-genBounds.y / 2, genBounds.y / 2),
                Random.Range(-genBounds.z / 2, genBounds.z / 2)
            );
            if (!IsVisibleByAllCameras(initialPoint))
                continue;
            initialFound = true;
            break;
        }

        if (!initialFound)
        {
            Debug.LogWarning("No initial visible point found within generation bounds.");
            return new List<Vector3>();
        }

        var initialCellX = (int)((initialPoint.x + genBounds.x / 2) / cellSize);
        var initialCellY = (int)((initialPoint.y + genBounds.y / 2) / cellSize);
        var initialCellZ = (int)((initialPoint.z + genBounds.z / 2) / cellSize);

        grid[initialCellX, initialCellY, initialCellZ] = initialPoint;
        points.Add(initialPoint);
        activeList.Add(initialPoint);

        while (activeList.Count > 0)
        {
            var randIndex = Random.Range(0, activeList.Count);
            var point = activeList[randIndex];
            var found = false;

            for (var i = 0; i < maxAttempts; i++)
            {
                var angle1 = Random.Range(0f, Mathf.PI * 2);
                var angle2 = Random.Range(0f, Mathf.PI * 2);
                var radius = Random.Range(minDist, 2 * minDist);

                var newPoint = point + new Vector3(
                    radius * Mathf.Sin(angle1) * Mathf.Cos(angle2),
                    radius * Mathf.Sin(angle1) * Mathf.Sin(angle2),
                    radius * Mathf.Cos(angle1)
                );

                if (!IsValidPoint(newPoint, grid, genBounds, minDist, cellSize) ||
                    !IsVisibleByAllCameras(newPoint))
                    continue;
                var cellX = (int)((newPoint.x + genBounds.x / 2) / cellSize);
                var cellY = (int)((newPoint.y + genBounds.y / 2) / cellSize);
                var cellZ = (int)((newPoint.z + genBounds.z / 2) / cellSize);

                grid[cellX, cellY, cellZ] = newPoint;
                points.Add(newPoint);
                activeList.Add(newPoint);
                found = true;
                break;
            }

            if (!found)
                activeList.RemoveAt(randIndex);
        }

        return points;
    }

    /// <summary>
    /// Validates whether a candidate point is within the bounds and maintains the minimum required distance from existing points.
    /// </summary>
    private static bool IsValidPoint(Vector3 point, Vector3[,,] grid, Vector3 genBounds, float minDist, float cellSize)
    {
        var cellX = (int)((point.x + genBounds.x / 2) / cellSize);
        var cellY = (int)((point.y + genBounds.y / 2) / cellSize);
        var cellZ = (int)((point.z + genBounds.z / 2) / cellSize);

        if (cellX < 0 || cellY < 0 || cellZ < 0 ||
            cellX >= grid.GetLength(0) || cellY >= grid.GetLength(1) || cellZ >= grid.GetLength(2))
        {
            return false;
        }

        const int searchRadius = 2;
        for (var x = Mathf.Max(0, cellX - searchRadius); x < Mathf.Min(grid.GetLength(0), cellX + searchRadius); x++)
        {
            for (var y = Mathf.Max(0, cellY - searchRadius); y < Mathf.Min(grid.GetLength(1), cellY + searchRadius); y++)
            {
                for (var z = Mathf.Max(0, cellZ - searchRadius); z < Mathf.Min(grid.GetLength(2), cellZ + searchRadius); z++)
                {
                    if (grid[x, y, z] != Vector3.zero && Vector3.Distance(grid[x, y, z], point) < minDist)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Computes the distorted viewport position for a given world point as the shader would.
    /// </summary>
    private static Vector2 GetDistortedViewportPosition(Camera cam, Vector3 worldPoint)
    {
        var camSpacePos = cam.worldToCameraMatrix.MultiplyPoint(worldPoint);

        if (camSpacePos.z >= 0)
            return new Vector2(-1, -1);

        var rXY = new Vector2(camSpacePos.x, camSpacePos.y).magnitude;
        var theta = Mathf.Atan2(rXY, -camSpacePos.z);
        var rUndist = Mathf.Tan(theta);

        var d = FisheyeAlpha / (1.0f - FisheyeAlpha);
        var t2 = FisheyeXi * Mathf.Cos(theta) +
                 Mathf.Sqrt(1.0f - FisheyeXi * FisheyeXi * Mathf.Sin(theta) * Mathf.Sin(theta));
        var t3 = FisheyeLambda * Mathf.Cos(theta) +
                 Mathf.Sqrt(1.0f - FisheyeLambda * FisheyeLambda * Mathf.Sin(theta) * Mathf.Sin(theta));
        var rDist = (t2 * t3 * Mathf.Sin(theta)) / (t2 * t3 * Mathf.Cos(theta) + d);
        var factor = (rUndist > 0.0001f) ? (rDist / rUndist) : 1.0f;

        var proj = cam.projectionMatrix;
        var clipPos = proj * new Vector4(camSpacePos.x, camSpacePos.y, camSpacePos.z, 1.0f);
        var ndc = new Vector2(clipPos.x, clipPos.y) / clipPos.w;

        // Apply the distortion factor.
        var ndcDistorted = ndc * factor;
        // Convert from normalized device coordinates (range [-1,1]) to viewport coordinates [0,1].
        var viewportPos = ndcDistorted * 0.5f + new Vector2(0.5f, 0.5f);
        return viewportPos;
    }

    /// <summary>
    /// Determines if a point is visible (including its full size with extra padding) by every active camera using the fisheye distortion.
    /// This method samples the candidate's center and additional offset points in the camera's tangent directions.
    /// </summary>
    private static bool IsVisibleByAllCameras(Vector3 point)
    {
        var cameras = Camera.allCameras;
        return cameras.Length != 0
               && (from cam in cameras
                   where cam.isActiveAndEnabled
                   let camRight = cam.transform.right
                   let camUp = cam.transform.up
                   let offsets = new[]
                   {
                       Vector3.zero,
                       camRight * BallRadius,
                       -camRight * BallRadius, camUp * BallRadius,
                       -camUp * BallRadius,
                       (camRight + camUp).normalized * BallRadius,
                       (camRight - camUp).normalized * BallRadius,
                       (-camRight + camUp).normalized * BallRadius,
                       (-camRight - camUp).normalized * BallRadius
                   } from offset
                       in offsets
                   let samplePoint = point + offset
                   select GetDistortedViewportPosition(cam, samplePoint))
               .All(viewportPos => !(viewportPos.x < ViewportPadding)
                                   && !(viewportPos.x > 1f - ViewportPadding)
                                   && !(viewportPos.y < ViewportPadding)
                                   && !(viewportPos.y > 1f - ViewportPadding));
    }
}
