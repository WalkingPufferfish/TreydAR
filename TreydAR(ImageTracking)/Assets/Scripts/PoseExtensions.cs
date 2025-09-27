// PoseExtensions.cs
using UnityEngine;

/// <summary>
/// This is a static helper class that adds new functionality (extension methods)
/// to Unity's built-in Pose struct.
/// </summary>
public static class PoseExtensions
{
    /// <summary>
    /// Calculates the inverse of a Pose. This is the transformation required
    /// to get from the Pose back to the origin.
    /// </summary>
    /// <param name="pose">The pose to invert.</param>
    /// <returns>The inverted pose.</returns>
    public static Pose Inverse(this Pose pose)
    {
        var rotation = Quaternion.Inverse(pose.rotation);
        var position = -(rotation * pose.position);
        return new Pose(position, rotation);
    }
}