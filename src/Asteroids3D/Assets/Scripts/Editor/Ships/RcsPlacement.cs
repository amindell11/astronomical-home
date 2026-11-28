using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ships
{
    /// <summary>
    /// Places a chassis's RCS sockets from its hull mesh: corners from the bow and stern flank
    /// extremes, roll nozzles from the mid flanks.
    /// Places only a new chassis whose sockets sit at the base's zero.
    /// </summary>
    public static class RcsPlacement
    {
        public static void Place(string chassisPrefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(chassisPrefabPath);
            try
            {
                var rcs = root.transform.Find("ShipBaseRig/Thruster/Rcs");
                if (rcs)
                    foreach (Transform socket in rcs)
                        if (socket.localPosition != Vector3.zero)
                            throw new InvalidOperationException($"{chassisPrefabPath} already places its RCS sockets; move them in the prefab.");

                var hull = root.transform.Find("ShipBaseRig/Hull");
                var vertices = new List<Vector3>();
                if (hull)
                {
                    foreach (var filter in hull.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var mesh = filter.sharedMesh;
                        if (!mesh) continue;
                        foreach (var vertex in mesh.vertices)
                            vertices.Add(root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
                    }
                }

                if (vertices.Count == 0)
                    throw new InvalidOperationException($"{chassisPrefabPath} has no hull vertices.");

                var yMin = float.PositiveInfinity;
                var yMax = float.NegativeInfinity;
                var xMin = float.PositiveInfinity;
                var xMax = float.NegativeInfinity;
                foreach (var vertex in vertices)
                {
                    if (vertex.y < yMin) yMin = vertex.y;
                    if (vertex.y > yMax) yMax = vertex.y;
                    if (vertex.x < xMin) xMin = vertex.x;
                    if (vertex.x > xMax) xMax = vertex.x;
                }

                var length = yMax - yMin;
                var width = xMax - xMin;
                var mid = (yMin + yMax) / 2f;

                (Vector3 left, Vector3 right) Band(float lo, float hi, string band)
                {
                    var any = false;
                    var atLeft = default(Vector3);
                    var atRight = default(Vector3);
                    foreach (var vertex in vertices)
                    {
                        if (vertex.y < lo || vertex.y > hi) continue;
                        if (!any || vertex.x < atLeft.x) atLeft = vertex;
                        if (!any || vertex.x > atRight.x) atRight = vertex;
                        any = true;
                    }

                    if (!any)
                        throw new InvalidOperationException($"{chassisPrefabPath} {band} band is empty.");
                    return (atLeft, atRight);
                }

                Vector3 Nudge(Vector3 point)
                {
                    point.x -= Mathf.Sign(point.x) * 0.03f * width;
                    return point;
                }

                void Set(string socketName, Vector3 point)
                {
                    var path = "ShipBaseRig/Thruster/Rcs/" + socketName;
                    var socket = root.transform.Find(path);
                    if (!socket)
                        throw new InvalidOperationException($"{chassisPrefabPath} is missing {path}.");
                    socket.position = root.transform.TransformPoint(point);
                }

                Vector3 Prow(float side)
                {
                    var best = default(Vector3);
                    var any = false;
                    foreach (var vertex in vertices)
                    {
                        if (vertex.x * side < 0.25f * width) continue;
                        if (any && vertex.y <= best.y) continue;
                        best = vertex;
                        any = true;
                    }

                    if (!any)
                        throw new InvalidOperationException($"{chassisPrefabPath} has no prow corner on side {side}.");
                    return best;
                }

                var bow = Band(yMax - 0.35f * length, yMax - 0.1f * length, "bow");
                var stern = Band(yMin + 0.1f * length, yMin + 0.35f * length, "stern");
                var flanks = Band(mid - 0.1f * length, mid + 0.1f * length, "mid");
                var bowLeft = Nudge(bow.left);
                var bowRight = Nudge(bow.right);
                var sternLeft = Nudge(stern.left);
                var sternRight = Nudge(stern.right);
                var midLeft = Nudge(flanks.left);
                var midRight = Nudge(flanks.right);

                Set("BowLeft", bowLeft);
                Set("BowForwardLeft", Prow(-1f));
                Set("BowRight", bowRight);
                Set("BowForwardRight", Prow(1f));
                Set("SternLeft", sternLeft);
                Set("SternRight", sternRight);
                Set("RollLeftTop", midLeft);
                Set("RollLeftBottom", midLeft);
                Set("RollRightTop", midRight);
                Set("RollRightBottom", midRight);

                PrefabUtility.SaveAsPrefabAsset(root, chassisPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
