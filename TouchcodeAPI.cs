﻿using MathNet.Numerics;
using MathNet.Spatial.Euclidean;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace TangibleTouch
{
    /// <summary>
    /// API to check for Touchcodes.
    /// </summary>
    public class TouchcodeAPI
    {
        private Dictionary<Point2D, int> _touchpointMap = new Dictionary<Point2D, int> {
            { new Point2D(1, 3), 0x001 },
            { new Point2D(2, 3), 0x002 },
            { new Point2D(0, 2), 0x004 },
            { new Point2D(1, 2), 0x008 },
            { new Point2D(2, 2), 0x010 },
            { new Point2D(3, 2), 0x020 },
            { new Point2D(0, 1), 0x040 },
            { new Point2D(1, 1), 0x080 },
            { new Point2D(2, 1), 0x100 },
            { new Point2D(3, 1), 0x200 },
            { new Point2D(1, 0), 0x400 },
            { new Point2D(2, 0), 0x800 },
        };


        /// <summary>
        /// Checks a list of <see cref="TouchPoint">TouchPoints</see> for the existence of a touchcode. 
        /// If the TouchPoints contained no Touchcode, Touchcode.None is returned.
        /// </summary>
        /// <param name="touchPoints">A list of <see cref="TouchPoint">TouchPoint</see> instances.</param>
        /// <param name="xMirror">A flag to enable or disable xMirroring. Set to true when the y coordinates of the screen 
        /// grow from top to bottom. Defaults to true.</param>
        /// <param name="maxY">The number of pixels on the y-axis of the screen. 
        /// Only needs to be set when <paramref name="xMirror"/> is set to true. Defaults to 1080.</param>
        /// <returns>A <see cref="Touchcode">Touchcode</see> instance.</returns>
        public Touchcode Check(IList<TouchPoint> touchPoints, bool xMirror = true, int maxY = 1080)
        {
            return Check(touchPoints.Select(point => new Point2D(point.Position.X, point.Position.Y)).ToList(), xMirror, maxY);
        }

        /// <summary>
        /// Checks a list of <see cref="Point2D">Points</see> for the existence of a touchcode. 
        /// If the Points contained no Touchcode, Touchcode.None is returned.
        /// </summary>
        /// <param name="touchPoints">A list of <see cref="Point2D"/> instances.</param>
        /// <param name="xMirror">A flag to enable or disable xMirroring. Set to true when the y coordinates of the screen 
        /// grow from top to bottom. Defaults to true.</param>
        /// <param name="maxY">The number of pixels on the y-axis of the screen. 
        /// Only needs to be set when <paramref name="xMirror"/> is set to true. Defaults to 1080.</param>
        /// <returns>A <see cref="Touchcode">Touchcode</see> instance.</returns>
        public Touchcode Check(IList<Point2D> touchpoints, bool xMirror = true, int maxY = 1080)
        {
            if (touchpoints == null || touchpoints.Count < 3)
            {
                return Touchcode.None;
            }

            if (xMirror)
            {
                touchpoints = MirrorX(touchpoints);
            }

            var referenceSystem = GetReferenceSystem(touchpoints);

            if (referenceSystem == null)
            {
                return Touchcode.None;
            }

            var touchcodeValue = MapPointsToTouchcode(touchpoints.Select(point => Normalize(referenceSystem, point)));


            var o = new Point2D(referenceSystem.Item1.X, 1080 - referenceSystem.Item1.Y);
            var y = new Point2D(referenceSystem.Item3.X, 1080 - referenceSystem.Item3.Y);

            var oy = o - y;
            var py = new Vector2D(0, 1);

            var angle = oy.SignedAngleTo(py, true);

            return new Touchcode(touchcodeValue, angle.Degrees, o);
        }


        public Tuple<Point2D, Vector2D, Vector2D> GetReferenceSystem(IList<Point2D> touchPoints)
        {
            double maxDeviationLength = 0.08;

            var longestDistance = touchPoints
                .Combinations(2)
                .Select(points => new Tuple<Point2D, Point2D, double>(points.ElementAt(0), points.ElementAt(1), points.ElementAt(0).DistanceTo(points.ElementAt(1))))
                .OrderByDescending(t => t.Item3)
                .FirstOrDefault();

            var v1 = longestDistance.Item1;
            var v2 = longestDistance.Item2;

            foreach (var point in touchPoints)
            {
                var pv1 = v1 - point;
                var pv2 = v2 - point;

                if (pv1.IsPerpendicularTo(pv2, 5.001)
                    && pv1.LengthAlmostEqual(longestDistance.Item3 / Constants.Sqrt2, maxDeviationLength)
                    && pv2.LengthAlmostEqual(longestDistance.Item3 / Constants.Sqrt2, maxDeviationLength))
                {
                    return FindVxVyIn(point, pv1, pv2);
                }
            }

            return null;
        }

        private Tuple<Point2D, Vector2D, Vector2D> FindVxVyIn(Point2D origin, Vector2D v1, Vector2D v2)
        {
            var positiveXAxis = new Vector2D(1, 0);
            var positiveYAxis = new Vector2D(0, 1);

            var angle = v1.SignedAngleTo(positiveYAxis, false, false);

            var vx = v2.Rotate(angle).HasSameOrientationAs(positiveXAxis) ? v2 : v1;
            var vy = (vx == v1) ? v2 : v1;

            return new Tuple<Point2D, Vector2D, Vector2D>(origin, vx, vy); 
        }

        private Point2D Normalize(Tuple<Point2D, Vector2D, Vector2D> referenceSystem, Point2D point)
        {
            var vx = referenceSystem.Item2;
            var vy = referenceSystem.Item3;
            var so = point - referenceSystem.Item1;

            vx = (vx / vx.Length) / vx.Length * 3;
            vy = (vy / vy.Length) / vy.Length * 3;

            var xcor = vx.DotProduct(so);
            var ycor = vy.DotProduct(so);

            return new Point2D(Math.Round(xcor, 1), Math.Round(ycor, 1));
        }

        public int MapPointsToTouchcode(IEnumerable<Point2D> touchPoints)
        {
            var threshold = 0.2001;
            var touchcode = 0;

            _touchpointMap.ToList().ForEach(map => touchcode |= touchPoints.Any(tp => tp.AlmostEqual(map.Key, threshold)) ? map.Value : 0);

            return touchcode;
        }

        public IList<Point2D> MirrorX(IEnumerable<Point2D> touchPoints, int maxY = 1080)
        {
            return touchPoints.Select(point => new Point2D(point.X, maxY - point.Y)).ToList();
        }

        public string Serialize(List<TouchPoint> touchpoints)
        {
            StringBuilder builder = new StringBuilder("[");

            touchpoints.ForEach(p => builder.AppendFormat("({0},{1}){2}", p.Position.X, p.Position.Y, p.Equals(touchpoints.Last()) ? "" : ","));

            return builder.Append("]").ToString();
        }
    }

    internal static class ExtensionMethods
    {
        public static IEnumerable<IEnumerable<T>> Combinations<T>(this IEnumerable<T> elements, int k)
        {
            return k == 0 ? new[] { new T[0] } :
              elements.SelectMany((e, i) =>
                elements.Skip(i + 1).Combinations(k - 1).Select(c => (new[] { e }).Concat(c)));
        }

        public static bool AlmostEqual(this Point2D thisPoint, Point2D thatPoint, double threshold)
        {
            return Precision.AlmostEqual(thisPoint.X, thatPoint.X, threshold) && Precision.AlmostEqual(thisPoint.Y, thatPoint.Y, threshold);
        }

        public static bool LengthAlmostEqual(this Vector2D thisPoint, double length, double thresholdInPercent)
        {
            return Precision.AlmostEqual(thisPoint.Length, length, length * thresholdInPercent);
        }

        public static bool HasSameOrientationAs(this Vector2D thisVector, Vector2D otherVector)
        {
            return thisVector.DotProduct(otherVector) > 0;
        }
    }
}
