using Hexa.NET.ImGui;
using System.Numerics;

namespace ImNodesCSharp.Math
{
    public struct Rectangle : IEquatable<Rectangle>
    {
        public float Left;
        public float Top;
        public float Right;
        public float Bottom;

        public Vector2 Min
        {
            get => new Vector2(Left, Top);
            set
            {
                Left = value.X;
                Top = value.Y;
            }
        }
        public Vector2 Max
        {
            get => new Vector2(Right, Bottom);
            set
            {
                Right = value.X;
                Bottom = value.Y;
            }
        }
        
        public Vector2 Size => Max - Min;

        //------------------------------------------------------------------------------
        public Rectangle()
        {
            Left = 0f;
            Top = 0f;
            Right = 0f;
            Bottom = 0f;
        }

        //------------------------------------------------------------------------------
        public Rectangle(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        ////------------------------------------------------------------------------------
        //public Rectangle(Vector2 pos, Vector2 size)
        //{
        //    Left = pos.X;
        //    Top = pos.Y;
        //    Right = pos.X + size.X;
        //    Bottom = pos.Y + size.Y;
        //}

        //------------------------------------------------------------------------------
        public ImRect ToImRect() => new ImRect(Min, Max);
        
        //------------------------------------------------------------------------------
        public bool IsEmpty() => Min.X >= Max.X || Min.Y >= Max.Y;

        //------------------------------------------------------------------------------
        public readonly bool Contains(Vector2 point)
        {
            if (point.X >= Left && point.X <= Right && point.Y >= Top)
            {
                return point.Y <= Bottom;
            }

            return false;
        }

        //------------------------------------------------------------------------------
        public readonly bool Contains(Rectangle other)
        {
            if (Left <= other.Left && Top <= other.Top && Right >= other.Right)
            {
                return Bottom >= other.Bottom;
            }

            return false;
        }

        //------------------------------------------------------------------------------
        public readonly bool Intersects(Rectangle other, out Rectangle intersection)
        {
            float num = System.Math.Max(Left, other.Left);
            float num2 = System.Math.Max(Top, other.Top);
            float num3 = System.Math.Min(Right, other.Right);
            float num4 = System.Math.Min(Bottom, other.Bottom);
            if (num < num3 && num2 < num4)
            {
                intersection = new Rectangle(num, num2, num3, num4);
                return true;
            }

            intersection = default(Rectangle);
            return false;
        }

        //------------------------------------------------------------------------------
        public readonly bool Intersects(Rectangle other)
        {
            if (Left < other.Right && Right > other.Left && Top < other.Bottom)
            {
                return Bottom > other.Top;
            }

            return false;
        }

        //------------------------------------------------------------------------------
        public readonly Rectangle Merge(Rectangle other)
        {
            return new Rectangle(System.Math.Min(Left, other.Left), System.Math.Min(Top, other.Top), System.Math.Max(Right, other.Right), System.Math.Max(Bottom, other.Bottom));
        }

        //------------------------------------------------------------------------------
        public override readonly bool Equals(object obj)
        {
            if (obj is Rectangle other)
            {
                return Equals(other);
            }

            return false;
        }

        //------------------------------------------------------------------------------
        public readonly bool Equals(Rectangle other)
        {
            if (Left == other.Left && Top == other.Top && Right == other.Right)
            {
                return Bottom == other.Bottom;
            }

            return false;
        }

        //------------------------------------------------------------------------------
        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Left, Top, Right, Bottom);
        }

        //------------------------------------------------------------------------------
        public static bool operator ==(Rectangle left, Rectangle right)
        {
            return left.Equals(right);
        }

        //------------------------------------------------------------------------------
        public static bool operator !=(Rectangle left, Rectangle right)
        {
            return !(left == right);
        }

        //------------------------------------------------------------------------------
        public static Rectangle operator +(Rectangle left, Rectangle right)
        {
            return new Rectangle(left.Left - right.Left, left.Top - right.Top, left.Right + right.Right, left.Bottom + right.Bottom);
        }

        //------------------------------------------------------------------------------
        public static Rectangle operator -(Rectangle left, Rectangle right)
        {
            return new Rectangle(left.Left + right.Left, left.Top + right.Top, left.Right - right.Right, left.Bottom - right.Bottom);
        }

        //------------------------------------------------------------------------------
        public static Rectangle operator +(Rectangle left, Vector2 right)
        {
            return new Rectangle(left.Left + right.X, left.Top + right.Y, left.Right + right.X, left.Bottom + right.Y);
        }

        //------------------------------------------------------------------------------
        public static Rectangle operator -(Rectangle left, Vector2 right)
        {
            return new Rectangle(left.Left - right.X, left.Top - right.Y, left.Right - right.X, left.Bottom - right.Y);
        }

        //------------------------------------------------------------------------------
        public static Rectangle operator +(Rectangle left, float right)
        {
            return new Rectangle((int)((float)left.Left + right), (int)((float)left.Top + right), (int)((float)left.Right + right), (int)((float)left.Bottom + right));
        }

        //------------------------------------------------------------------------------
        public static Rectangle operator -(Rectangle left, float right)
        {
            return new Rectangle((int)((float)left.Left - right), (int)((float)left.Top - right), (int)((float)left.Right - right), (int)((float)left.Bottom - right));
        }

        //------------------------------------------------------------------------------
        public static Rectangle operator *(Rectangle left, float right)
        {
            return new Rectangle((int)((float)left.Left * right), (int)((float)left.Top * right), (int)((float)left.Right * right), (int)((float)left.Bottom * right));
        }

        //------------------------------------------------------------------------------
        public static Rectangle operator /(Rectangle left, float right)
        {
            return new Rectangle((int)((float)left.Left / right), (int)((float)left.Top / right), (int)((float)left.Right / right), (int)((float)left.Bottom / right));
        }

        //------------------------------------------------------------------------------
        public static Rectangle operator +(Rectangle left, int right)
        {
            return new Rectangle(left.Left + right, left.Top + right, left.Right + right, left.Bottom + right);
        }

        //------------------------------------------------------------------------------
        public static Rectangle operator -(Rectangle left, int right)
        {
            return new Rectangle(left.Left - right, left.Top - right, left.Right - right, left.Bottom - right);
        }

        //------------------------------------------------------------------------------
        public static Rectangle operator *(Rectangle left, int right)
        {
            return new Rectangle(left.Left * right, left.Top * right, left.Right * right, left.Bottom * right);
        }

        //------------------------------------------------------------------------------
        public static Rectangle operator /(Rectangle left, int right)
        {
            return new Rectangle(left.Left / right, left.Top / right, left.Right / right, left.Bottom / right);
        }

        //------------------------------------------------------------------------------
        public override readonly string ToString()
        {
            return $"Left: {Left}, Top: {Top}, Right: {Right}, Bottom: {Bottom}";
        }

        // Rectangle interface
        public Vector2 GetCenter() => new Vector2((Left + Right) * 0.5f, (Top + Bottom) * 0.5f);
        public Vector2 GetSize() => Max - Min;
        public float GetWidth() => Right - Left;
        public float GetHeight() => Bottom - Top;
        public float GetArea() => (Max.X - Min.X) * (Max.Y - Min.Y);
        public Vector2 GetTL() => Min;                   // Top-left
        public Vector2 GetTR() => new Vector2(Max.X, Min.Y);  // Top-right
        public Vector2 GetBL() => new Vector2(Min.X, Max.Y);  // Bottom-left
        public Vector2 GetBR() => Max;                   // Bottom-right
        //bool Contains(Vector2 p) => p.X >= Min.X && p.Y >= Min.Y && p.X <  Max.X && p.Y <  Max.Y;
        //bool Contains(Rectangle r) => r.Min.X >= Min.X && r.Min.Y >= Min.Y && r.Max.X <= Max.X && r.Max.Y <= Max.Y;
        public bool Overlaps(Rectangle r) => r.Min.Y <  Max.Y && r.Max.Y >  Min.Y && r.Min.X <  Max.X && r.Max.X >  Min.X;
        public void Add(Vector2 p) { if (Min.X > p.X) Left = p.X; if (Min.Y > p.Y) Top = p.Y; if (Max.X < p.X) Right = p.X; if (Max.Y < p.Y) Bottom = p.Y; }
        public void Add(Rectangle r) { if (Min.X > r.Min.X) Left = r.Min.X; if (Min.Y > r.Min.Y) Top = r.Min.Y; if (Max.X < r.Max.X) Right = r.Max.X; if (Max.Y < r.Max.Y) Bottom = r.Max.Y; }
        public void Expand(float amount) { Left -= amount; Top -= amount; Right += amount; Bottom += amount; }
        public void Expand(Vector2 amount) { Left -= amount.X; Top -= amount.Y; Right += amount.X; Bottom += amount.Y; }
        public void Translate(Vector2 d) { Left += d.X; Top += d.Y; Right += d.X; Bottom += d.Y; }
        public void TranslateX(float dx) { Left += dx; Right += dx; }
        public void TranslateY(float dy) { Top += dy; Bottom += dy; }
        public void ClipWith(Rectangle r) { Left = System.Math.Max(Left, r.Left); Top = System.Math.Max(Top, r.Top); Right = System.Math.Min(Right, r.Right); Bottom = System.Math.Min(Bottom, r.Bottom); }  // Simple version, may lead to an inverted rectangle, which is fine for Contains/Overlaps test but not for display.
        //public void ClipWithFull(Rectangle r) { Min = ImClamp(Min, r.Min, r.Max); Max = ImClamp(Max, r.Min, r.Max); } // Full version, ensure both points are fully clipped.
        public void Floor() { Min = ExtraMath.ImFloor(Min); Max = ExtraMath.ImFloor(Max); }
        public bool IsInverted() => Min.X > Max.X || Min.Y > Max.Y;
        public Vector4 ToVec4() => new Vector4(Min.X, Min.Y, Max.X, Max.Y);



        //------------------------------------------------------------------------------
        public Vector2 ClosestPoint(Vector2 p, bool snap_to_edge)
        {
            if (!snap_to_edge && Contains(p))
                return p;

            return new Vector2(
                (p.X > Max.X) ? Max.X : (p.X < Min.X ? Min.X : p.X),
                (p.Y > Max.Y) ? Max.Y : (p.Y < Min.Y ? Min.Y : p.Y)
            );
        }

        //------------------------------------------------------------------------------
        public Vector2 ClosestPoint(Vector2 p, bool snap_to_edge, float radius)
        {
            Vector2 point = ClosestPoint(p, snap_to_edge);

            Vector2 offset = p - point;
            double distance_sq = offset.X * offset.X + offset.Y * offset.Y;
            if (distance_sq <= 0.0)
                return point;

            float distance = (float)System.Math.Sqrt(distance_sq);

            return point + offset * (System.Math.Min(distance, radius) * (1.0f / distance));
        }

        //------------------------------------------------------------------------------
        public Vector2 ClosestPoint(Rectangle other)
        {
            Vector2 result;
            if (other.Min.X >= Max.X)
                result.X = Max.X;
            else if (other.Max.X <= Min.X)
                result.X = Min.X;
            else
                result.X = (System.Math.Max(Min.X, other.Min.X) + System.Math.Min(Max.X, other.Max.X)) / 2;

            if (other.Min.Y >= Max.Y)
                result.Y = Max.Y;
            else if (other.Max.Y <= Min.Y)
                result.Y = Min.Y;
            else
                result.Y = (System.Math.Max(Min.Y, other.Min.Y) + System.Math.Min(Max.Y, other.Max.Y)) / 2;
            return result;
        }

        //------------------------------------------------------------------------------
        public void distribute(ref float a, ref float b, float a0, float a1, float b0, float b1)
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

        //------------------------------------------------------------------------------
        public ImLine ClosestLine(Rectangle rect)
        {
            ImLine result;
            result.A = ClosestPoint(rect);
            result.B = rect.ClosestPoint(this);

            distribute(ref result.A.X, ref result.B.X, Min.X, Max.X, rect.Min.X, rect.Max.X);
            distribute(ref result.A.Y, ref result.B.Y, Min.Y, Max.Y, rect.Min.Y, rect.Max.Y);

            return result;
        }

        //------------------------------------------------------------------------------
        public ImLine ClosestLine(Rectangle rect, float radius_a, float radius_b)
        {
            ImLine line = ClosestLine(rect);
            if (radius_a < 0)
                radius_a = 0;
            if (radius_b < 0)
                radius_b = 0;

            if (radius_a == 0 && radius_b == 0)
                return line;

            Vector2 offset = line.B - line.A;
            float length_sq = offset.X * offset.X + offset.Y * offset.Y;
            float radius_a_sq = radius_a * radius_a;
            float radius_b_sq = radius_b * radius_b;

            if (length_sq <= 0)
                return line;

            float length = MathF.Sqrt(length_sq);
            Vector2 direction = new Vector2(offset.X / length, offset.Y / length);

            float total_radius_sq = radius_a_sq + radius_b_sq;
            if (total_radius_sq > length_sq)
            {
                float scale = length / (radius_a + radius_b);
                radius_a *= scale;
                radius_b *= scale;
            }

            line.A = line.A + (direction * radius_a);
            line.B = line.B - (direction * radius_b);

            return line;
        }
    }
}
