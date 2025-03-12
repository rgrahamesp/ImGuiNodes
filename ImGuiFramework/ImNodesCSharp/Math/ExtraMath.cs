using Hexa.NET.ImGui;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;

namespace ImNodesCSharp.Math
{
    // A simple structure representing a line segment.
    public struct ImLine
    {
        public Vector2 A;
        public Vector2 B;

        public ImLine(Vector2 a, Vector2 b)
        {
            A = a;
            B = b;
        }
    }

    // Example helper structs corresponding to QuadOffsets and TriangleOffsets.
    public struct QuadOffsets
    {
        public Vector2 TopLeft;
        public Vector2 BottomLeft;
        public Vector2 BottomRight;
        public Vector2 TopRight;
    }

    public struct TriangleOffsets
    {
        public Vector2 TopLeft;
        public Vector2 BottomLeft;
        public Vector2 Right;
    }

    //-------------------------------------------------------------------------
    // Extra math helper functions (replacing the inline functions from imgui_extra_math.inl)
    public static class ExtraMath
    {
        // If not already defined as operators in Vector2, you can define these as extension methods.
        // For example, you might add:
        // public static bool ApproximatelyEquals(this Vector2 lhs, Vector2 rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y;
        // public static Vector2 Negate(this Vector2 v) => new Vector2(-v.X, -v.Y);
        // etc.

        // For scalars, ImLength(v) just returns the absolute value.
        public static float ImLength(float v) => v;

        public static float ImLength(Vector2 v) =>
            (float)System.Math.Sqrt(ImLengthSqr(v));

        public static float ImLengthSqr(float v) => v * v;

        public static float ImLengthSqr(Vector2 v) => v.X * v.X + v.Y * v.Y;

        public static float ImSqrt(float v) => (float)System.Math.Sqrt(v);

        public static float ImFabs(float v) => System.Math.Abs(v);

        public static float ImPow(float v, float p) => (float)System.Math.Pow(v, p);

        public static float ImMin(float a, float b) => System.Math.Min(a, b);

        public static float ImMax(float a, float b) => System.Math.Max(a, b);

        public static void ImSwap<T>(ref T a, ref T b) { T tmp = a; a = b; b = tmp; }

        public static Vector2 ImFloor(Vector2 v) => new Vector2((float)System.Math.Floor(v.X), (float)System.Math.Floor(v.Y));

        public static float ImCeil(float v) => (float)System.Math.Ceiling(v);

        public static Vector2 ImCeil(Vector2 v) => new Vector2((float)System.Math.Ceiling(v.X), (float)System.Math.Ceiling(v.Y));

        public static Vector2 ImNormalized(Vector2 v) => v * ImInvLength(v, 0.0f);


        // Returns the inverse of the length of vector v, or 0 if v is too small.
        //-------------------------------------------------------------------------
        public static float ImInvLength(Vector2 v, float epsilon = 0.0f)
        {
            float len = ImLength(v);
            return (len > epsilon) ? 1.0f / len : 0;
        }

        //-------------------------------------------------------------------------
        public static Vector2 ImLineClosestPoint(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ap = p - a;
            Vector2 ab_dir = b - a;
            float dot = ap.X * ab_dir.X + ap.Y * ab_dir.Y;
            if (dot < 0.0f)
                return a;
            float ab_len_sqr = ab_dir.X * ab_dir.X + ab_dir.Y * ab_dir.Y;
            if (dot > ab_len_sqr)
                return b;
            return a + ab_dir * dot / ab_len_sqr;
        }

        //-------------------------------------------------------------------------
        public static int Sign(float val)
        {
            int a = (val > 0.0f) ? 1 : 0;
            int b = (val < 0.0f) ? 1 : 0;
            return a - b;
        }

        //-------------------------------------------------------------------------
        // Rectangle helper functions
        //-------------------------------------------------------------------------

        //-------------------------------------------------------------------------
        public static bool ImRect_IsEmpty(Rectangle rect)
        {
            return rect.Left >= rect.Right || rect.Top >= rect.Bottom;
        }

        /// <summary>
        /// Returns the closest point on the rectangle 'rect' to point 'p'.
        /// If snapToEdge is false and p is contained in rect, returns p.
        /// </summary>
        //-------------------------------------------------------------------------
        public static Vector2 ImRect_ClosestPoint(Rectangle rect, Vector2 p, bool snapToEdge)
        {
            if (!snapToEdge && rect.Contains(p))
                return p;

            float x = (p.X > rect.Right) ? rect.Right : (p.X < rect.Left ? rect.Left : p.X);
            float y = (p.Y > rect.Bottom) ? rect.Bottom : (p.Y < rect.Top ? rect.Top : p.Y);
            return new Vector2(x, y);
        }

        /// <summary>
        /// Returns the closest point on rect to p, then pulls that point toward p by up to radius.
        /// </summary>
        //-------------------------------------------------------------------------
        public static Vector2 ImRect_ClosestPoint(Rectangle rect, Vector2 p, bool snapToEdge, float radius)
        {
            Vector2 point = ImRect_ClosestPoint(rect, p, snapToEdge);
            Vector2 offset = p - point;
            float distanceSq = offset.X * offset.X + offset.Y * offset.Y;
            if (distanceSq <= 0)
                return point;
            float distance = ImSqrt(distanceSq);
            float factor = ImMin(distance, radius) / distance;
            return point + offset * factor;
        }

        /// <summary>
        /// Returns a point that is “centrally” located between rect and another rectangle 'other'.
        /// </summary>
        //-------------------------------------------------------------------------
        public static Vector2 ImRect_ClosestPoint(Rectangle rect, Rectangle other)
        {
            float x;
            if (other.Left >= rect.Right)
                x = rect.Right;
            else if (other.Right <= rect.Left)
                x = rect.Left;
            else
                x = (System.Math.Max(rect.Left, other.Left) + System.Math.Min(rect.Right, other.Right)) / 2;

            float y;
            if (other.Top >= rect.Bottom)
                y = rect.Bottom;
            else if (other.Bottom <= rect.Top)
                y = rect.Top;
            else
                y = (System.Math.Max(rect.Top, other.Top) + System.Math.Min(rect.Bottom, other.Bottom)) / 2;

            return new Vector2(x, y);
        }

        /// <summary>
        /// Returns a line (with endpoints A and B) connecting the closest points between rectA and rectB.
        /// </summary>
        //-------------------------------------------------------------------------
        public static ImLine ImRect_ClosestLine(Rectangle rectA, Rectangle rectB)
        {
            ImLine result;
            result.A = ImRect_ClosestPoint(rectA, rectB);
            result.B = ImRect_ClosestPoint(rectB, rectA);

            // Local helper lambda.
            void Distribute(ref float a, ref float b, float a0, float a1, float b0, float b1)
            {
                if (a0 >= b1 || a1 <= b0)
                    return;
                float aw = a1 - a0;
                float bw = b1 - b0;
                if (aw > bw)
                {
                    b = b0 + bw - bw * (a - a0) / aw;
                    a = b;
                }
                else if (aw < bw)
                {
                    a = a0 + aw - aw * (b - b0) / bw;
                    b = a;
                }
            }

            Distribute(ref result.A.X, ref result.B.X, rectA.Left, rectA.Right, rectB.Left, rectB.Right);
            Distribute(ref result.A.Y, ref result.B.Y, rectA.Top, rectA.Bottom, rectB.Top, rectB.Bottom);
            return result;
        }

        /// <summary>
        /// Returns a line connecting the closest points between two rectangles with an offset applied based on radii.
        /// </summary>
        //-------------------------------------------------------------------------
        public static ImLine ImRect_ClosestLine(Rectangle rectA, Rectangle rectB, float radiusA, float radiusB)
        {
            ImLine line = ImRect_ClosestLine(rectA, rectB);
            if (radiusA < 0) radiusA = 0;
            if (radiusB < 0) radiusB = 0;
            if (radiusA == 0 && radiusB == 0)
                return line;

            Vector2 offset = line.B - line.A;
            float lengthSq = offset.X * offset.X + offset.Y * offset.Y;
            if (lengthSq <= 0)
                return line;

            float length = ImSqrt(lengthSq);
            Vector2 direction = new Vector2(offset.X / length, offset.Y / length);
            if (radiusA + radiusB > length)
            {
                float scale = length / (radiusA + radiusB);
                radiusA *= scale;
                radiusB *= scale;
            }
            line.A = line.A + direction * radiusA;
            line.B = line.B - direction * radiusB;
            return line;
        }
    }

    //-------------------------------------------------------------------------
    // Easing functions
    public static class ImEasing
    {
        /// <summary>
        /// Easing function: EaseOutQuad.
        /// Returns: b - c*(t*(t-2))
        /// </summary>
        public static float EaseOutQuad(float b, float c, float t)
        {
            return b - c * (t * (t - 2));
        }
    }
}
