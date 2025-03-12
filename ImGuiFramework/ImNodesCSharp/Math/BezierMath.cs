using Hexa.NET.ImGui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace ImNodesCSharp.Math
{
    // Assumed types (you may already have these defined elsewhere)
    // public struct Vector2 { public float x, y; … } with operator overloads, Dot(), Length(), Min(), Max(), etc.
    // public struct Rectangle { public Vector2 Min, Max; public Rectangle(Vector2 min, Vector2 max) { Min = min; Max = max; } }
    // public struct ImLine { public Vector2 A, B; }

    // Struct representing four control points of a cubic Bezier curve.
    public struct CubicBezierPoints
    {
        public Vector2 P0;
        public Vector2 P1;
        public Vector2 P2;
        public Vector2 P3;

        public CubicBezierPoints(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;
        }
    }

    // Result for splitting a cubic Bezier curve.
    public struct CubicBezierSplitResult
    {
        public CubicBezierPoints Left;
        public CubicBezierPoints Right;
    }

    // Result for projecting a point on a cubic Bezier curve.
    public struct ProjectResult
    {
        public Vector2 Point;    // Closest point on the curve.
        public float Time;      // Parameter t in [0,1].
        public float Distance;  // Distance from the given point.
    }

    // Result for line/curve intersection.
    public struct CubicBezierIntersectResult
    {
        public int Count;
        public Vector2[] Points; // Up to 3 intersection points.
    }

    // Flags for subdivision.
    [Flags]
    public enum CubicBezierSubdivideFlags
    {
        None = 0,
        SkipFirst = 1
    }

    // Sample data provided by the adaptive subdivision routines.
    public struct CubicBezierSubdivideSample
    {
        public Vector2 Point;
        public Vector2 Tangent;
    }

    // Delegate for adaptive subdivision callback.
    public delegate void CubicBezierSubdivideCallback(CubicBezierSubdivideSample sample);

    // Sample data provided by the fixed–step subdivision routines.
    public struct CubicBezierFixedStepSample
    {
        public float T;
        public float Length;
        public Vector2 Point;
        public bool BreakSearch;
    }

    // Delegate for fixed–step subdivision callback.
    public delegate void CubicBezierFixedStepCallback(ref CubicBezierFixedStepSample sample);

    // Static class that contains all Bezier–math functions.
    public static class BezierMath
    {
        //------------------------------------------------------------------------
        // Low-level Bezier sampling

        public static Vector2 LinearBezier(Vector2 p0, Vector2 p1, float t)
        {
            return p0 + t * (p1 - p0);
        }

        public static Vector2 LinearBezierDt(Vector2 p0, Vector2 p1, float t)
        {
            // t is unused
            return p1 - p0;
        }

        public static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float a = 1 - t;
            return (a * a) * p0 + (2 * t * a) * p1 + (t * t) * p2;
        }

        public static Vector2 QuadraticBezierDt(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            return 2 * (1 - t) * (p1 - p0) + 2 * t * (p2 - p1);
        }

        public static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float a = 1 - t;
            float b = a * a * a;
            float c = t * t * t;
            return b * p0 + 3 * t * a * a * p1 + 3 * t * t * a * p2 + c * p3;
        }

        public static Vector2 CubicBezierDt(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float a = 1 - t;
            float b = a * a;
            float c = t * t;
            float d = 2 * t * a;
            return -3 * b * p0 + 3 * (b - d) * p1 + 3 * (d - c) * p2 + 3 * c * p3;
        }

        // High-level sampling that collapses to a lower order if control points overlap.
        public static Vector2 CubicBezierSample(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            bool cp0_zero = (p1 - p0).LengthSquared() < 1e-5f;
            bool cp1_zero = (p3 - p2).LengthSquared() < 1e-5f;

            if (cp0_zero && cp1_zero)
                return LinearBezier(p0, p3, t);
            else if (cp0_zero)
                return QuadraticBezier(p0, p2, p3, t);
            else if (cp1_zero)
                return QuadraticBezier(p0, p1, p3, t);
            else
                return CubicBezier(p0, p1, p2, p3, t);
        }

        public static Vector2 CubicBezierSample(CubicBezierPoints curve, float t)
        {
            return CubicBezierSample(curve.P0, curve.P1, curve.P2, curve.P3, t);
        }

        public static Vector2 CubicBezierTangent(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            bool cp0_zero = (p1 - p0).LengthSquared() < 1e-5f;
            bool cp1_zero = (p3 - p2).LengthSquared() < 1e-5f;

            if (cp0_zero && cp1_zero)
                return LinearBezierDt(p0, p3, t);
            else if (cp0_zero)
                return QuadraticBezierDt(p0, p2, p3, t);
            else if (cp1_zero)
                return QuadraticBezierDt(p0, p1, p3, t);
            else
                return CubicBezierDt(p0, p1, p2, p3, t);
        }

        public static Vector2 CubicBezierTangent(CubicBezierPoints curve, float t)
        {
            return CubicBezierTangent(curve.P0, curve.P1, curve.P2, curve.P3, t);
        }

        //------------------------------------------------------------------------
        // Approximate length of a cubic Bezier curve (using 24-point Legendre-Gauss quadrature).
        public static float CubicBezierLength(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float[] tValues = new float[]
            {
                -0.064056892862605626f,  0.064056892862605626f,
                -0.19111886747361631f,   0.19111886747361631f,
                -0.31504267969616337f,   0.31504267969616337f,
                -0.43379350762604514f,   0.43379350762604514f,
                -0.54542147138883954f,   0.54542147138883954f,
                -0.64809365193697557f,   0.64809365193697557f,
                -0.74012419157855436f,   0.74012419157855436f,
                -0.82000198597390292f,   0.82000198597390292f,
                -0.88641552700440103f,   0.88641552700440103f,
                -0.93827455200273276f,   0.93827455200273276f,
                -0.9747285559713095f,    0.9747285559713095f,
                -0.99518721999702136f,   0.99518721999702136f
            };

            float[] cValues = new float[]
            {
                0.12793819534675216f,  0.12793819534675216f,
                0.1258374563468283f,   0.1258374563468283f,
                0.12167047292780339f,  0.12167047292780339f,
                0.1155056680537256f,   0.1155056680537256f,
                0.10744427011596563f,  0.10744427011596563f,
                0.097618652104113888f, 0.097618652104113888f,
                0.086190161531953275f, 0.086190161531953275f,
                0.073346481411080306f, 0.073346481411080306f,
                0.05929858491543678f,  0.05929858491543678f,
                0.044277438817419806f, 0.044277438817419806f,
                0.028531388628933664f, 0.028531388628933664f,
                0.0123412297999872f,   0.0123412297999872f
            };

            int n = tValues.Length;
            float z = 0.5f;
            float accumulator = 0.0f;
            for (int i = 0; i < n; i++)
            {
                float t = z * tValues[i] + z;
                Vector2 d = CubicBezierDt(p0, p1, p2, p3, t);
                float l = d.Length();
                accumulator += cValues[i] * l;
            }
            return z * accumulator;
        }

        public static float CubicBezierLength(CubicBezierPoints curve)
        {
            return CubicBezierLength(curve.P0, curve.P1, curve.P2, curve.P3);
        }

        //------------------------------------------------------------------------
        // Split a cubic Bezier curve at parameter t.
        public static CubicBezierSplitResult CubicBezierSplit(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float z1 = t;
            float z2 = z1 * z1;
            float z3 = z1 * z1 * z1;
            float s1 = z1 - 1;
            float s2 = s1 * s1;
            float s3 = s1 * s1 * s1;

            CubicBezierPoints left = new CubicBezierPoints
            {
                P0 = p0,
                P1 = z1 * p1 - s1 * p0,
                P2 = z2 * p2 - 2 * z1 * s1 * p1 + s2 * p0,
                P3 = z3 * p3 - 3 * z2 * s1 * p2 + 3 * z1 * s2 * p1 - s3 * p0
            };

            CubicBezierPoints right = new CubicBezierPoints
            {
                P0 = z3 * p0 - 3 * z2 * s1 * p1 + 3 * z1 * s2 * p2 - s3 * p3,
                P1 = z2 * p1 - 2 * z1 * s1 * p2 + s2 * p3,
                P2 = z1 * p2 - s1 * p3,
                P3 = p3
            };

            return new CubicBezierSplitResult { Left = left, Right = right };
        }

        public static CubicBezierSplitResult CubicBezierSplit(CubicBezierPoints curve, float t)
        {
            return CubicBezierSplit(curve.P0, curve.P1, curve.P2, curve.P3, t);
        }

        //------------------------------------------------------------------------
        // Bounding rectangle of a cubic Bezier curve.
        // Also defines a helper for scalar cubic evaluation.
        private static float CubicBezier(float p0, float p1, float p2, float p3, float t)
        {
            float a = 1 - t;
            return a * a * a * p0 + 3 * t * a * a * p1 + 3 * t * t * a * p2 + t * t * t * p3;
        }

        public static Rectangle CubicBezierBoundingRect(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            Vector2 a = 3 * p3 - 9 * p2 + 9 * p1 - 3 * p0;
            Vector2 b = 6 * p0 - 12 * p1 + 6 * p2;
            Vector2 c = 3 * p1 - 3 * p0;

            // Start with the box from endpoints.
            Vector2 tl = Vector2.Min(p0, p3);
            Vector2 rb = Vector2.Max(p0, p3);

            // Check each coordinate.
            for (int i = 0; i < 2; i++)
            {
                float ai = (i == 0 ? a.X : a.Y);
                if (ai == 0.0f)
                    continue;
                float bi = (i == 0 ? b.X : b.Y);
                float ci = (i == 0 ? c.X : c.Y);
                float deltaSquared = bi * bi - 4 * ai * ci;
                if (deltaSquared >= 0)
                {
                    float delta = (float)System.Math.Sqrt(deltaSquared);
                    float t0 = (-bi + delta) / (2 * ai);
                    if (t0 > 0 && t0 < 1)
                    {
                        float p = CubicBezier(i == 0 ? p0.X : p0.Y, i == 0 ? p1.X : p1.Y, i == 0 ? p2.X : p2.Y, i == 0 ? p3.X : p3.Y, t0);
                        if (i == 0)
                        {
                            tl.X = System.Math.Min(tl.X, p);
                            rb.X = System.Math.Max(rb.X, p);
                        }
                        else
                        {
                            tl.Y = System.Math.Min(tl.Y, p);
                            rb.Y = System.Math.Max(rb.Y, p);
                        }
                    }
                    float t1 = (-bi - delta) / (2 * ai);
                    if (t1 > 0 && t1 < 1)
                    {
                        float p = CubicBezier(i == 0 ? p0.X : p0.Y, i == 0 ? p1.X : p1.Y, i == 0 ? p2.X : p2.Y, i == 0 ? p3.X : p3.Y, t1);
                        if (i == 0)
                        {
                            tl.X = System.Math.Min(tl.X, p);
                            rb.X = System.Math.Max(rb.X, p);
                        }
                        else
                        {
                            tl.Y = System.Math.Min(tl.Y, p);
                            rb.Y = System.Math.Max(rb.Y, p);
                        }
                    }
                }
            }
            return new Rectangle(tl, rb);
        }

        public static Rectangle CubicBezierBoundingRect(CubicBezierPoints curve)
        {
            return CubicBezierBoundingRect(curve.P0, curve.P1, curve.P2, curve.P3);
        }

        //------------------------------------------------------------------------
        // Project a point onto a cubic Bezier curve.
        public static ProjectResult ProjectOnCubicBezier(Vector2 point, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int subdivisions = 100)
        {
            const float epsilon = 1e-5f;
            float fixedStep = 1.0f / (subdivisions - 1);
            ProjectResult result = new ProjectResult
            {
                Point = point,
                Time = 0.0f,
                Distance = float.MaxValue
            };

            // Coarse search.
            for (int i = 0; i < subdivisions; i++)
            {
                float t = i * fixedStep;
                Vector2 pOnCurve = CubicBezier(p0, p1, p2, p3, t);
                float d = Vector2.Dot(point - pOnCurve, point - pOnCurve);
                if (d < result.Distance)
                {
                    result.Point = pOnCurve;
                    result.Time = t;
                    result.Distance = d;
                }
            }

            if (result.Time == 0.0f || System.Math.Abs(result.Time - 1.0f) <= epsilon)
            {
                result.Distance = (float)System.Math.Sqrt(result.Distance);
                return result;
            }

            // Fine search.
            float left = result.Time - fixedStep;
            float right = result.Time + fixedStep;
            float step = fixedStep * 0.1f;
            for (float t = left; t < right + step; t += step)
            {
                Vector2 pOnCurve = CubicBezier(p0, p1, p2, p3, t);
                float d = Vector2.Dot(point - pOnCurve, point - pOnCurve);
                if (d < result.Distance)
                {
                    result.Point = pOnCurve;
                    result.Time = t;
                    result.Distance = d;
                }
            }
            result.Distance = (float)System.Math.Sqrt(result.Distance);
            return result;
        }

        public static ProjectResult ProjectOnCubicBezier(Vector2 p, CubicBezierPoints curve, int subdivisions = 100)
        {
            return ProjectOnCubicBezier(p, curve.P0, curve.P1, curve.P2, curve.P3, subdivisions);
        }

        //------------------------------------------------------------------------
        // Intersection between a line and a cubic Bezier curve.
        public static CubicBezierIntersectResult CubicBezierLineIntersect(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 a0, Vector2 a1)
        {
            // Compute cubic coefficients in Bernstein form.
            Vector2 c3 = -p0 + 3 * p1 - 3 * p2 + p3;
            Vector2 c2 = 3 * p0 - 6 * p1 + 3 * p2;
            Vector2 c1 = -3 * p0 + 3 * p1;
            Vector2 c0 = p0;

            // Convert line to its normal form: A*x + B*y + C = 0.
            float A = a1.Y - a0.Y;
            float B = a0.X - a1.X;
            float C = a0.X * (a0.Y - a1.Y) + a0.Y * (a1.X - a0.X);

            // Rotate each coefficient.
            float rc3 = A * c3.X + B * c3.Y;
            float rc2 = A * c2.X + B * c2.Y;
            float rc1 = A * c1.X + B * c1.Y;
            float rc0 = A * c0.X + B * c0.Y + C;

            // Solve cubic: rc3*t^3 + rc2*t^2 + rc1*t + rc0 = 0.
            float[] roots = new float[3];
            int rootCount = CubicRoots(rc3, rc2, rc1, rc0, roots);

            Vector2 min = Vector2.Min(a0, a1);
            Vector2 max = Vector2.Max(a0, a1);
            List<Vector2> pts = new List<Vector2>();

            for (int i = 0; i < rootCount; i++)
            {
                float t = roots[i];
                if (t >= 0 && t <= 1)
                {
                    Vector2 p = CubicBezier(p0, p1, p2, p3, t);
                    // Check if p lies on the line segment.
                    if (a0.X == a1.X)
                    {
                        if (p.Y >= min.Y && p.Y <= max.Y)
                            pts.Add(p);
                    }
                    else if (a0.Y == a1.Y)
                    {
                        if (p.X >= min.X && p.X <= max.X)
                            pts.Add(p);
                    }
                    else if (p.X >= min.X && p.Y >= min.Y && p.X <= max.X && p.Y <= max.Y)
                    {
                        pts.Add(p);
                    }
                }
            }

            CubicBezierIntersectResult res = new CubicBezierIntersectResult
            {
                Count = pts.Count,
                Points = new Vector2[3]
            };
            for (int i = 0; i < System.Math.Min(3, pts.Count); i++)
            {
                res.Points[i] = pts[i];
            }
            return res;
        }

        public static CubicBezierIntersectResult CubicBezierLineIntersect(CubicBezierPoints curve, ImLine line)
        {
            return CubicBezierLineIntersect(curve.P0, curve.P1, curve.P2, curve.P3, line.A, line.B);
        }

        // Helper to solve cubic equations.
        // Returns the number of (real) roots found, and fills the roots array.
        private static int CubicRoots(float a, float b, float c, float d, float[] roots)
        {
            int count = 0;
            Func<float, float> sign = x => x < 0 ? -1.0f : 1.0f;

            float A = b / a;
            float B = c / a;
            float C = d / a;

            float Q = (3 * B - A * A) / 9;
            float R = (9 * A * B - 27 * C - 2 * A * A * A) / 54;
            float Dval = Q * Q * Q + R * R; // discriminant

            if (Dval >= 0)
            {
                float S = sign(R + (float)System.Math.Sqrt(Dval)) * (float)System.Math.Pow(System.Math.Abs(R + (float)System.Math.Sqrt(Dval)), 1.0 / 3.0);
                float T = sign(R - (float)System.Math.Sqrt(Dval)) * (float)System.Math.Pow(System.Math.Abs(R - (float)System.Math.Sqrt(Dval)), 1.0 / 3.0);

                roots[0] = -A / 3 + (S + T);
                roots[1] = -A / 3 - (S + T) / 2;
                roots[2] = roots[1];
                float Im = System.Math.Abs((float)(System.Math.Sqrt(3) * (S - T) / 2));
                count = (Im != 0) ? 1 : 3;
            }
            else
            {
                float th = (float)System.Math.Acos(R / (float)System.Math.Sqrt(-Q * Q * Q));
                roots[0] = 2 * (float)System.Math.Sqrt(-Q) * (float)System.Math.Cos(th / 3) - A / 3;
                roots[1] = 2 * (float)System.Math.Sqrt(-Q) * (float)System.Math.Cos((th + 2 * System.Math.PI) / 3) - A / 3;
                roots[2] = 2 * (float)System.Math.Sqrt(-Q) * (float)System.Math.Cos((th + 4 * System.Math.PI) / 3) - A / 3;
                count = 3;
            }
            return count;
        }

        //------------------------------------------------------------------------
        // Adaptive cubic Bezier subdivision.
        public static void CubicBezierSubdivide(CubicBezierSubdivideCallback callback, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float tessTol = -1.0f, CubicBezierSubdivideFlags flags = CubicBezierSubdivideFlags.None)
        {
            // Delegate to the overload accepting a CubicBezierPoints.
            CubicBezierSubdivide(callback, new CubicBezierPoints(p0, p1, p2, p3), tessTol, flags);
        }

        /// <summary>
        /// Draws a cubic Bezier curve with optional arrowheads.
        /// </summary>
        /// <param name="drawList">The draw list to add the drawing commands to.</param>
        /// <param name="curve">The cubic Bezier curve (with points P0..P3).</param>
        /// <param name="thickness">The base thickness for the curve.</param>
        /// <param name="startArrowSize">Size of the start arrow (0 if none).</param>
        /// <param name="startArrowWidth">Width of the start arrow.</param>
        /// <param name="endArrowSize">Size of the end arrow (0 if none).</param>
        /// <param name="endArrowWidth">Width of the end arrow.</param>
        /// <param name="fill">If true, draw a filled shape (and arrowheads) instead of a stroked outline.</param>
        /// <param name="color">The drawing color (with alpha in the high byte).</param>
        /// <param name="strokeThickness">The stroke thickness for the outline (if not filled).</param>
        /// <param name="startDirHint">Optional hint for the start direction.</param>
        /// <param name="endDirHint">Optional hint for the end direction.</param>
        //------------------------------------------------------------------------
        public static void AddBezierWithArrows(
            ImDrawListPtr drawList,
            CubicBezierPoints curve,
            float thickness,
            float startArrowSize,
            float startArrowWidth,
            float endArrowSize,
            float endArrowWidth,
            bool fill,
            uint color,
            float strokeThickness,
            Vector2? startDirHint = null,
            Vector2? endDirHint = null)
        {
            // If alpha is 0, skip drawing.
            if ((color >> 24) == 0)
                return;

            float halfThickness = thickness * 0.5f;

            if (fill)
            {
                drawList.AddBezierCubic(curve.P0, curve.P1, curve.P2, curve.P3, color, thickness);

                if (startArrowSize > 0.0f)
                {
                    // Use the provided startDirHint if available; otherwise, compute tangent at t=0.
                    Vector2 startDir = ExtraMath.ImNormalized(startDirHint.HasValue
                        ? startDirHint.Value
                        : BezierMath.CubicBezierTangent(curve.P0, curve.P1, curve.P2, curve.P3, 0.0f));
                    Vector2 startN = new Vector2(-startDir.Y, startDir.X);
                    float halfWidth = startArrowWidth * 0.5f;
                    Vector2 tip = curve.P0 - startDir * startArrowSize;

                    drawList.PathLineTo(curve.P0 - startN * System.Math.Max(halfWidth, halfThickness));
                    drawList.PathLineTo(curve.P0 + startN * System.Math.Max(halfWidth, halfThickness));
                    drawList.PathLineTo(tip);
                    drawList.PathFillConvex(color);
                }

                if (endArrowSize > 0.0f)
                {
                    // If endDirHint is provided, use its negated value; otherwise, compute tangent at t=1.
                    Vector2 endDir = ExtraMath.ImNormalized(endDirHint.HasValue
                        ? -endDirHint.Value
                        : BezierMath.CubicBezierTangent(curve.P0, curve.P1, curve.P2, curve.P3, 1.0f));
                    Vector2 endN = new Vector2(-endDir.Y, endDir.X);
                    float halfWidth = endArrowWidth * 0.5f;
                    Vector2 tip = curve.P3 + endDir * endArrowSize;

                    drawList.PathLineTo(curve.P3 + endN * System.Math.Max(halfWidth, halfThickness));
                    drawList.PathLineTo(curve.P3 - endN * System.Math.Max(halfWidth, halfThickness));
                    drawList.PathLineTo(tip);
                    drawList.PathFillConvex(color);
                }
            }
            else
            {
                if (startArrowSize > 0.0f)
                {
                    Vector2 startDir = ExtraMath.ImNormalized(BezierMath.CubicBezierTangent(curve.P0, curve.P1, curve.P2, curve.P3, 0.0f));
                    Vector2 startN = new Vector2(-startDir.Y, startDir.X);
                    float halfWidth = startArrowWidth * 0.5f;
                    Vector2 tip = curve.P0 - startDir * startArrowSize;

                    if (halfWidth > halfThickness)
                        drawList.PathLineTo(curve.P0 - startN * halfWidth);
                    drawList.PathLineTo(tip);
                    if (halfWidth > halfThickness)
                        drawList.PathLineTo(curve.P0 + startN * halfWidth);
                }

                // Call our helper function to offset the bezier path.
                PathBezierOffset(drawList, halfThickness, curve.P0, curve.P1, curve.P2, curve.P3);

                if (endArrowSize > 0.0f)
                {
                    Vector2 endDir = ExtraMath.ImNormalized(BezierMath.CubicBezierTangent(curve.P0, curve.P1, curve.P2, curve.P3, 1.0f));
                    Vector2 endN = new Vector2(-endDir.Y, endDir.X);
                    float halfWidth = endArrowWidth * 0.5f;
                    Vector2 tip = curve.P3 + endDir * endArrowSize;

                    if (halfWidth > halfThickness)
                        drawList.PathLineTo(curve.P3 + endN * halfWidth);
                    drawList.PathLineTo(tip);
                    if (halfWidth > halfThickness)
                        drawList.PathLineTo(curve.P3 - endN * halfWidth);
                }

                // Draw the reverse offset bezier.
                PathBezierOffset(drawList, halfThickness, curve.P3, curve.P2, curve.P1, curve.P0);
                drawList.PathStroke(color, ImDrawFlags.Closed, strokeThickness);
            }
        }

        //------------------------------------------------------------------------
        public static void PathBezierOffset(ImDrawListPtr drawList, float offset, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            // Subdivide the cubic Bezier curve and call the lambda for each sample.
            CubicBezierSubdivide((sample) =>
            {
                // Calculate the perpendicular vector of the tangent.
                Vector2 perpendicular = ExtraMath.ImNormalized(new Vector2(-sample.Tangent.Y, sample.Tangent.X));
                // Multiply by offset and add to the sample point.
                Vector2 newPoint = sample.Point + perpendicular * offset;
                // Add the resulting point to the current path.
                drawList.PathLineTo(newPoint);
            },
            p0, p1, p2, p3);
        }


        //------------------------------------------------------------------------
        public static void CubicBezierSubdivide(CubicBezierSubdivideCallback callback, CubicBezierPoints curve, float tessTol = -1.0f, CubicBezierSubdivideFlags flags = CubicBezierSubdivideFlags.None)
        {
            if (tessTol < 0)
                tessTol = 1.118f; // sqrt(1.25)

            // Local helper: commit a sample.
            void Commit(Vector2 p, Vector2 t)
            {
                CubicBezierSubdivideSample sample = new CubicBezierSubdivideSample { Point = p, Tangent = t };
                callback(sample);
            }

            // Recursive subdivision.
            void Subdivide(CubicBezierPoints c, int level)
            {
                float dx = c.P3.X - c.P0.X;
                float dy = c.P3.Y - c.P0.Y;
                float d2 = System.Math.Abs((c.P1.X - c.P3.X) * dy - (c.P1.Y - c.P3.Y) * dx);
                float d3 = System.Math.Abs((c.P2.X - c.P3.X) * dy - (c.P2.Y - c.P3.Y) * dx);
                if ((d2 + d3) * (d2 + d3) < tessTol * (dx * dx + dy * dy))
                {
                    Commit(c.P3, CubicBezierTangent(c, 1.0f));
                }
                else if (level < 10)
                {
                    Vector2 p12 = (c.P0 + c.P1) * 0.5f;
                    Vector2 p23 = (c.P1 + c.P2) * 0.5f;
                    Vector2 p34 = (c.P2 + c.P3) * 0.5f;
                    Vector2 p123 = (p12 + p23) * 0.5f;
                    Vector2 p234 = (p23 + p34) * 0.5f;
                    Vector2 p1234 = (p123 + p234) * 0.5f;

                    Subdivide(new CubicBezierPoints(c.P0, p12, p123, p1234), level + 1);
                    Subdivide(new CubicBezierPoints(p1234, p234, p34, c.P3), level + 1);
                }
            }

            if ((flags & CubicBezierSubdivideFlags.SkipFirst) == 0)
                Commit(curve.P0, CubicBezierTangent(curve, 0.0f));
            Subdivide(curve, 0);
        }

        //------------------------------------------------------------------------
        // Fixed–step cubic Bezier subdivision.
        //------------------------------------------------------------------------
        public static void CubicBezierFixedStep(CubicBezierFixedStepCallback callback, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float step, bool overshoot = false, float maxValueError = 1e-3f, float maxTError = 1e-5f)
        {
            if (step <= 0.0f || callback == null || maxValueError <= 0 || maxTError <= 0)
                return;

            CubicBezierFixedStepSample sample = new CubicBezierFixedStepSample
            {
                T = 0.0f,
                Length = 0.0f,
                Point = p0,
                BreakSearch = false
            };

            callback(ref sample);
            if (sample.BreakSearch)
                return;

            float totalLength = CubicBezierLength(p0, p1, p2, p3);
            int pointCount = (int)(totalLength / step) + (overshoot ? 2 : 1);
            float tMin = 0.0f;
            float tMax = step * pointCount / totalLength;
            float t0 = (tMin + tMax) * 0.5f;

            Dictionary<float, float> cache = new Dictionary<float, float>();
            for (int pointIndex = 1; pointIndex < pointCount; pointIndex++)
            {
                float targetLength = pointIndex * step;
                float tStart = tMin;
                float tEnd = tMax;
                float t = t0;
                float tBest = t;
                float errorBest = totalLength;

                while (true)
                {
                    if (!cache.TryGetValue(t, out float length))
                    {
                        CubicBezierSplitResult split = CubicBezierSplit(p0, p1, p2, p3, t);
                        length = CubicBezierLength(split.Left);
                        cache[t] = length;
                    }
                    float error = targetLength - length;
                    if (error < errorBest)
                    {
                        errorBest = error;
                        tBest = t;
                    }

                    if (System.Math.Abs(error) <= maxValueError || System.Math.Abs(tStart - tEnd) <= maxTError)
                    {
                        sample.T = t;
                        sample.Length = length;
                        sample.Point = CubicBezier(p0, p1, p2, p3, t);
                        callback(ref sample);
                        if (sample.BreakSearch)
                            return;
                        break;
                    }
                    else if (error < 0.0f)
                        tEnd = t;
                    else
                        tStart = t;
                    t = (tStart + tEnd) * 0.5f;
                }
            }
        }

        public static void CubicBezierFixedStep(CubicBezierFixedStepCallback callback, CubicBezierPoints curve, float step, bool overshoot = false, float maxValueError = 1e-3f, float maxTError = 1e-5f)
        {
            CubicBezierFixedStep(callback, curve.P0, curve.P1, curve.P2, curve.P3, step, overshoot, maxValueError, maxTError);
        }
    }

    //-------------------------------------------------------------------------
    // New imnodes bezier functions
    //-------------------------------------------------------------------------

    //-------------------------------------------------------------------------
    public class CubicBezier
    {
        public Vector2 P0, P1, P2, P3;
        public int NumSegments;

        //-------------------------------------------------------------------------
        public static Vector2 EvalCubicBezier(float t, Vector2 P0, Vector2 P1, Vector2 P2, Vector2 P3)
        {
            // B(t) = (1-t)**3 p0 + 3(1 - t)**2 t P1 + 3(1-t)t**2 P2 + t**3 P3

            float u = 1.0f - t;
            float b0 = u * u * u;
            float b1 = 3 * u * u * t;
            float b2 = 3 * u * t * t;
            float b3 = t * t * t;
            return new Vector2(
                b0 * P0.X + b1 * P1.X + b2 * P2.X + b3 * P3.X,
                b0 * P0.Y + b1 * P1.Y + b2 * P2.Y + b3 * P3.Y);
        }

        // Calculates the closest point along each bezier curve segment.
        //-------------------------------------------------------------------------
        public static Vector2 GetClosestPointOnCubicBezier(int num_segments, Vector2 p, CubicBezier cb)
        {
            Debug.Assert(num_segments > 0);
            Vector2 p_last = cb.P0;
            Vector2 p_closest = Vector2.Zero;
            float p_closest_dist = float.MaxValue;
            float t_step = 1.0f / (float)num_segments;
            for (int i = 1; i <= num_segments; ++i)
            {
                Vector2 p_current = EvalCubicBezier(t_step * i, cb.P0, cb.P1, cb.P2, cb.P3);
                Vector2 p_line = ExtraMath.ImLineClosestPoint(p_last, p_current, p);
                float dist = ExtraMath.ImLengthSqr(p - p_line);
                if (dist < p_closest_dist)
                {
                    p_closest = p_line;
                    p_closest_dist = dist;
                }
                p_last = p_current;
            }

            return p_closest;
        }

        //-------------------------------------------------------------------------
        public static float GetDistanceToCubicBezier(Vector2 pos, CubicBezier cubic_bezier, int num_segments)
        {
            Vector2 point_on_curve = GetClosestPointOnCubicBezier(num_segments, pos, cubic_bezier);
            Vector2 to_curve = point_on_curve - pos;
            return ExtraMath.ImSqrt(ExtraMath.ImLengthSqr(to_curve));
        }

        //-------------------------------------------------------------------------
        public static Rectangle GetContainingRectForCubicBezier(CubicBezier cb, float hoverDistance)
        {
            Vector2 min = new Vector2(System.Math.Min(cb.P0.X, cb.P3.X), System.Math.Min(cb.P0.Y, cb.P3.Y));
            Vector2 max = new Vector2(System.Math.Max(cb.P0.X, cb.P3.X), System.Math.Max(cb.P0.Y, cb.P3.Y));

            Rectangle rect = new Rectangle(min, max);
            rect.Add(cb.P1);
            rect.Add(cb.P2);
            rect.Expand(new Vector2(hoverDistance, hoverDistance));

            return rect;
        }

        //-------------------------------------------------------------------------
        public static CubicBezier GetCubicBezier(Vector2 start, Vector2 end, ImNodesAttributeType start_type, float line_segments_per_length)
        {
            Debug.Assert((start_type == ImNodesAttributeType.Input) || (start_type == ImNodesAttributeType.Output));
            if (start_type == ImNodesAttributeType.Input)
            {
                ExtraMath.ImSwap<Vector2>(ref start, ref end);
            }

            float link_length = ExtraMath.ImSqrt(ExtraMath.ImLengthSqr(end - start));
            Vector2 offset = new Vector2(0.25f * link_length, 0f);
            CubicBezier cubic_bezier = new CubicBezier();
            cubic_bezier.P0 = start;
            cubic_bezier.P1 = start + offset;
            cubic_bezier.P2 = end - offset;
            cubic_bezier.P3 = end;
            cubic_bezier.NumSegments = System.Math.Max((int)(link_length * line_segments_per_length), 1);
            return cubic_bezier;
        }

        /// <summary>
        /// Returns true if the rectangle overlaps the line segment from p1 to p2.
        /// </summary>
        //-------------------------------------------------------------------------
        public static bool RectangleOverlapsLineSegment(Rectangle rect, Vector2 p1, Vector2 p2)
        {
            // Trivial case: if the rectangle contains either endpoint.
            if (rect.Contains(p1) || rect.Contains(p2))
                return true;

            // Make a copy of rect that we can "flip" if needed.
            Rectangle flipRect = rect;
            // Ensure Min.X <= Max.X.
            if (flipRect.Min.X > flipRect.Max.X)
            {
                float temp = flipRect.Min.X;
                flipRect.Min = new Vector2(flipRect.Max.X, flipRect.Min.Y);
                flipRect.Max = new Vector2(temp, flipRect.Max.Y);
            }
            // Ensure Min.Y <= Max.Y.
            if (flipRect.Min.Y > flipRect.Max.Y)
            {
                float temp = flipRect.Min.Y;
                flipRect.Min = new Vector2(flipRect.Min.X, flipRect.Max.Y);
                flipRect.Max = new Vector2(flipRect.Max.X, temp);
            }

            // Trivial rejection: if the entire segment lies to one side of the rectangle.
            if ((p1.X < flipRect.Min.X && p2.X < flipRect.Min.X) ||
                (p1.X > flipRect.Max.X && p2.X > flipRect.Max.X) ||
                (p1.Y < flipRect.Min.Y && p2.Y < flipRect.Min.Y) ||
                (p1.Y > flipRect.Max.Y && p2.Y > flipRect.Max.Y))
            {
                return false;
            }

            // Compute signs for the four corners relative to the line passing through p1 and p2.
            int[] cornerSigns = new int[4];
            cornerSigns[0] = System.Math.Sign(EvalImplicitLineEq(p1, p2, flipRect.Min));
            cornerSigns[1] = System.Math.Sign(EvalImplicitLineEq(p1, p2, new Vector2(flipRect.Max.X, flipRect.Min.Y)));
            cornerSigns[2] = System.Math.Sign(EvalImplicitLineEq(p1, p2, new Vector2(flipRect.Min.X, flipRect.Max.Y)));
            cornerSigns[3] = System.Math.Sign(EvalImplicitLineEq(p1, p2, flipRect.Max));

            int sum = 0;
            int sumAbs = 0;
            for (int i = 0; i < 4; i++)
            {
                sum += cornerSigns[i];
                sumAbs += System.Math.Abs(cornerSigns[i]);
            }

            // If all corners are on the same side, then |sum| == sumAbs.
            return System.Math.Abs(sum) != sumAbs;
        }

        //-------------------------------------------------------------------------
        public static float EvalImplicitLineEq(Vector2 p1, Vector2 p2, Vector2 p)
        {
            return (p2.Y - p1.Y) * p.X + (p1.X - p2.X) * p.Y + (p2.X * p1.Y - p1.X * p2.Y);
        }

        /// <summary>
        /// Returns true if the given Bézier curve overlaps the rectangle.
        /// The curve is approximated by sampling NumSegments+1 points.
        /// </summary>
        //-------------------------------------------------------------------------
        public static bool RectangleOverlapsBezier(Rectangle rectangle, CubicBezier cubicBezier)
        {
            Vector2 current = EvalCubicBezier(0f, cubicBezier.P0, cubicBezier.P1, cubicBezier.P2, cubicBezier.P3);
            float dt = 1.0f / cubicBezier.NumSegments;
            for (int s = 0; s < cubicBezier.NumSegments; s++)
            {
                float t = (s + 1) * dt;
                Vector2 next = EvalCubicBezier(t, cubicBezier.P0, cubicBezier.P1, cubicBezier.P2, cubicBezier.P3);
                if (RectangleOverlapsLineSegment(rectangle, current, next))
                    return true;
                current = next;
            }
            return false;
        }
    }
}
