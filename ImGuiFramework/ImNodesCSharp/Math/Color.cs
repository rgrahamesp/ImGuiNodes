using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImNodesCSharp.Math
{
    public class Color
    {
        public static Color Black = new Color(0f, 0f, 0f);
        public static Color White = new Color(1f, 1f, 1f);
        public static Color Red = new Color(1f, 0f, 0f);
        public static Color Green = new Color(0f, 1f, 0f);
        public static Color Blue = new Color(0f, 0f, 1f);
        public static Color Yellow = new Color(1f, 1f, 0f);
        public static Color Cyan = new Color(0f, 1f, 1f);
        public static Color Magenta = new Color(1f, 0f, 1f);
        public static Color Gray = new Color(0.5f, 0.5f, 0.5f);
        public static Color Clear = new Color(0f, 0f, 0f, 0f);

        public float r;
        public float g;
        public float b;
        public float a;

        //--------------------------------------------------------------------------
        public Color()
        {
        }

        //--------------------------------------------------------------------------
        public Color(float r, float g, float b, float a = 1.0f)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        //--------------------------------------------------------------------------
        public static Color HSVToRGB(float h, float s, float v)
        {
            float r = 0, g = 0, b = 0;
            if (s == 0)
            {
                r = g = b = v;
            }
            else
            {
                h *= 6;
                int i = (int)h;
                float f = h - i;
                float p = v * (1 - s);
                float q = v * (1 - s * f);
                float t = v * (1 - s * (1 - f));
                switch (i % 6)
                {
                    case 0:
                        r = v;
                        g = t;
                        b = p;
                        break;
                    case 1:
                        r = q;
                        g = v;
                        b = p;
                        break;
                    case 2:
                        r = p;
                        g = v;
                        b = t;
                        break;
                    case 3:
                        r = p;
                        g = q;
                        b = v;
                        break;
                    case 4:
                        r = t;
                        g = p;
                        b = v;
                        break;
                    case 5:
                        r = v;
                        g = p;
                        b = q;
                        break;
                }
            }
            return new Color(r, g, b);
        }

        //--------------------------------------------------------------------------
        public static void RGBToHSV(Color rgb, out float h, out float s, out float v)
        {
            float min = System.Math.Min(rgb.r, System.Math.Min(rgb.g, rgb.b));
            float max = System.Math.Max(rgb.r, System.Math.Max(rgb.g, rgb.b));
            float delta = max - min;
            v = max;
            s = max == 0 ? 0 : delta / max;
            if (s == 0)
            {
                h = 0;
            }
            else
            {
                if (rgb.r == max)
                    h = (rgb.g - rgb.b) / delta;
                else if (rgb.g == max)
                    h = 2 + (rgb.b - rgb.r) / delta;
                else
                    h = 4 + (rgb.r - rgb.g) / delta;
                h *= 60;
                if (h < 0)
                    h += 360;
            }
        }

        //--------------------------------------------------------------------------
        public static Color Lerp(Color a, Color b, float t)
        {
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t);
        }

        //--------------------------------------------------------------------------
        public static Color operator +(Color a, Color b)
        {
            return new Color(a.r + b.r, a.g + b.g, a.b + b.b);
        }

        //--------------------------------------------------------------------------
        public static Color operator -(Color a, Color b)
        {
            return new Color(a.r - b.r, a.g - b.g, a.b - b.b);
        }

        //--------------------------------------------------------------------------

        public static Color operator *(Color a, float b)
        {
            return new Color(a.r * b, a.g * b, a.b * b);
        }

        //--------------------------------------------------------------------------
        public static Color operator /(Color a, float b)
        {
            return new Color(a.r / b, a.g / b, a.b / b);
        }

        //--------------------------------------------------------------------------
        public static bool operator ==(Color a, Color b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b;
        }

        //--------------------------------------------------------------------------

        public static bool operator !=(Color a, Color b)
        {
            return a.r != b.r || a.g != b.g || a.b != b.b;
        }

        //--------------------------------------------------------------------------
        public override bool Equals(object obj)
        {
            if (obj is Color)
            {
                Color c = (Color)obj;
                return r == c.r && g == c.g && b == c.b;
            }
            return false;
        }

        //--------------------------------------------------------------------------
        public override int GetHashCode()
        {
            return r.GetHashCode() ^ g.GetHashCode() ^ b.GetHashCode();
        }

        //--------------------------------------------------------------------------
        public override string ToString()
        {
            return $"Color({r}, {g}, {b})";
        }

        //--------------------------------------------------------------------------
        public static Color Parse(string s)
        {
            Color color = null;

            string[] parts = s.Split(',');
            if (parts.Length == 3)
            {
                float r = float.Parse(parts[0]);
                float g = float.Parse(parts[1]);
                float b = float.Parse(parts[2]);
                color = new Color(r, g, b);
            }
            else if (parts.Length == 4)
            {
                float r = float.Parse(parts[0]);
                float g = float.Parse(parts[1]);
                float b = float.Parse(parts[2]);
                float a = float.Parse(parts[3]);
                color = new Color(r, g, b, a);
            }

            return color;
        }

        //--------------------------------------------------------------------------
        public static Color ParseHex(string hex)
        {
            Color color = null;

            if (hex.StartsWith("#"))
                hex = hex.Substring(1);

            if (hex.Length == 6)
            {
                int r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                int g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                int b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                color = new Color(r / 255f, g / 255f, b / 255f);
            }
            else if (hex.Length == 8)
            {
                int r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                int g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                int b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                int a = int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
            }

            return color;
        }

        //--------------------------------------------------------------------------
        public string ToHex()
        {
            return "#" + ((int)(r * 255)).ToString("X2") + ((int)(g * 255)).ToString("X2") + ((int)(b * 255)).ToString("X2");
        }

        //--------------------------------------------------------------------------

        public string ToRGB()
        {
            return $"rgb({(int)(r * 255)}, {(int)(g * 255)}, {(int)(b * 255)})";
        }

        //--------------------------------------------------------------------------
        public string ToRGBA()
        {
            return $"rgba({(int)(r * 255)}, {(int)(g * 255)}, {(int)(b * 255)}, {(int)(a * 255)})";
        }

        //--------------------------------------------------------------------------
        public static Color WashOut(string hexColor, float percent)
        {
            Color color = ParseHex(hexColor);
            if (color != null)
                color = WashOut(color, percent);
            //else
            //    Logger.LogError($"Failed to convert hex color string {hexColor}");

            return color;
        }

        //--------------------------------------------------------------------------
        public static Color WashOut(Color color, float percent)
        {
            float h, s, v;
            RGBToHSV(color, out h, out s, out v);
            return HSVToRGB(h, s * (1f - percent), v * (1f - percent));
        }

        //--------------------------------------------------------------------------
        public static Color Brighten(Color color, float percent)
        {
            float h, s, v;
            RGBToHSV(color, out h, out s, out v);
            return HSVToRGB(h, s * percent, v);
        }

        //--------------------------------------------------------------------------
        public static string WashOutHex(string hexColor, float percent)
        {
            string result = "#ffffff";
            Color color = ParseHex(hexColor);
            if (color != null)
                result = WashOutHex(color, percent);
            //else
            //    Logger.LogError($"Failed to convert hex color string {hexColor}");
            return result;
        }

        //--------------------------------------------------------------------------
        public static string WashOutHex(Color color, float percent)
        {
            return WashOut(color, percent).ToHex();
        }

        //--------------------------------------------------------------------------
        public static Color Darken(string hexColor, float percent)
        {
            Color color = ParseHex(hexColor);
            if (color != null)
                color = Darken(color, percent);
            //else
            //    Logger.LogError($"Failed to convert hex color string {hexColor}");

            return color;
        }

        //--------------------------------------------------------------------------
        public static Color Darken(Color color, float percent)
        {
            float h, s, v;
            RGBToHSV(color, out h, out s, out v);
            return HSVToRGB(h, s, v * (1f - percent));
        }
    }
}
