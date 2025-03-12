using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImNodesCSharp
{
    public static class NodeStylePresets
    {
        // Helper to pack RGBA into a uint in IM_COL32 format.
        public static uint IM_COL32(byte r, byte g, byte b, byte a)
        {
            // Assuming little-endian ordering: r is in the lowest byte.
            return ((uint)a << 24) | ((uint)b << 16) | ((uint)g << 8) | r;
        }

        /// <summary>
        /// Applies the dark style preset to the given style.
        /// If dest is null, applies to the global GImNodes.Style.
        /// </summary>
        public static void StyleColorsDark(ImNodesStyle dest)
        {
            dest.Colors[(int)ImNodesCol.NodeBackground] = IM_COL32(50, 50, 50, 255);
            dest.Colors[(int)ImNodesCol.NodeBackgroundHovered] = IM_COL32(75, 75, 75, 255);
            dest.Colors[(int)ImNodesCol.NodeBackgroundSelected] = IM_COL32(75, 75, 75, 255);
            dest.Colors[(int)ImNodesCol.NodeOutline] = IM_COL32(100, 100, 100, 255);
            dest.Colors[(int)ImNodesCol.TitleBar] = IM_COL32(41, 74, 122, 255);
            dest.Colors[(int)ImNodesCol.TitleBarHovered] = IM_COL32(66, 150, 250, 255);
            dest.Colors[(int)ImNodesCol.TitleBarSelected] = IM_COL32(66, 150, 250, 255);
            dest.Colors[(int)ImNodesCol.Link] = IM_COL32(61, 133, 224, 200);
            dest.Colors[(int)ImNodesCol.LinkHovered] = IM_COL32(66, 150, 250, 255);
            dest.Colors[(int)ImNodesCol.LinkSelected] = IM_COL32(66, 150, 250, 255);
            dest.Colors[(int)ImNodesCol.Pin] = IM_COL32(53, 150, 250, 180);
            dest.Colors[(int)ImNodesCol.PinHovered] = IM_COL32(53, 150, 250, 255);
            dest.Colors[(int)ImNodesCol.BoxSelector] = IM_COL32(61, 133, 224, 30);
            dest.Colors[(int)ImNodesCol.BoxSelectorOutline] = IM_COL32(61, 133, 224, 150);
            dest.Colors[(int)ImNodesCol.GridBackground] = IM_COL32(40, 40, 50, 200);
            dest.Colors[(int)ImNodesCol.GridLine] = IM_COL32(200, 200, 200, 40);
            dest.Colors[(int)ImNodesCol.GridLinePrimary] = IM_COL32(240, 240, 240, 60);
            // Minimaps
            dest.Colors[(int)ImNodesCol.MiniMapBackground] = IM_COL32(25, 25, 25, 150);
            dest.Colors[(int)ImNodesCol.MiniMapBackgroundHovered] = IM_COL32(25, 25, 25, 200);
            dest.Colors[(int)ImNodesCol.MiniMapOutline] = IM_COL32(150, 150, 150, 100);
            dest.Colors[(int)ImNodesCol.MiniMapOutlineHovered] = IM_COL32(150, 150, 150, 200);
            dest.Colors[(int)ImNodesCol.MiniMapNodeBackground] = IM_COL32(200, 200, 200, 100);
            dest.Colors[(int)ImNodesCol.MiniMapNodeBackgroundHovered] = IM_COL32(200, 200, 200, 255);
            // For selected nodes, use the hovered color.
            dest.Colors[(int)ImNodesCol.MiniMapNodeBackgroundSelected] = dest.Colors[(int)ImNodesCol.MiniMapNodeBackgroundHovered];
            dest.Colors[(int)ImNodesCol.MiniMapNodeOutline] = IM_COL32(200, 200, 200, 100);
            dest.Colors[(int)ImNodesCol.MiniMapLink] = dest.Colors[(int)ImNodesCol.Link];
            dest.Colors[(int)ImNodesCol.MiniMapLinkSelected] = dest.Colors[(int)ImNodesCol.LinkSelected];
            dest.Colors[(int)ImNodesCol.MiniMapCanvas] = IM_COL32(200, 200, 200, 25);
            dest.Colors[(int)ImNodesCol.MiniMapCanvasOutline] = IM_COL32(200, 200, 200, 200);
        }

        /// <summary>
        /// Applies the classic style preset.
        /// </summary>
        public static void StyleColorsClassic(ImNodesStyle dest)
        {
            dest.Colors[(int)ImNodesCol.NodeBackground] = IM_COL32(50, 50, 50, 255);
            dest.Colors[(int)ImNodesCol.NodeBackgroundHovered] = IM_COL32(75, 75, 75, 255);
            dest.Colors[(int)ImNodesCol.NodeBackgroundSelected] = IM_COL32(75, 75, 75, 255);
            dest.Colors[(int)ImNodesCol.NodeOutline] = IM_COL32(100, 100, 100, 255);
            dest.Colors[(int)ImNodesCol.TitleBar] = IM_COL32(69, 69, 138, 255);
            dest.Colors[(int)ImNodesCol.TitleBarHovered] = IM_COL32(82, 82, 161, 255);
            dest.Colors[(int)ImNodesCol.TitleBarSelected] = IM_COL32(82, 82, 161, 255);
            dest.Colors[(int)ImNodesCol.Link] = IM_COL32(255, 255, 255, 100);
            dest.Colors[(int)ImNodesCol.LinkHovered] = IM_COL32(105, 99, 204, 153);
            dest.Colors[(int)ImNodesCol.LinkSelected] = IM_COL32(105, 99, 204, 153);
            dest.Colors[(int)ImNodesCol.Pin] = IM_COL32(89, 102, 156, 170);
            dest.Colors[(int)ImNodesCol.PinHovered] = IM_COL32(102, 122, 179, 200);
            dest.Colors[(int)ImNodesCol.BoxSelector] = IM_COL32(82, 82, 161, 100);
            dest.Colors[(int)ImNodesCol.BoxSelectorOutline] = IM_COL32(82, 82, 161, 255);
            dest.Colors[(int)ImNodesCol.GridBackground] = IM_COL32(40, 40, 50, 200);
            dest.Colors[(int)ImNodesCol.GridLine] = IM_COL32(200, 200, 200, 40);
            dest.Colors[(int)ImNodesCol.GridLinePrimary] = IM_COL32(240, 240, 240, 60);
            // Minimaps
            dest.Colors[(int)ImNodesCol.MiniMapBackground] = IM_COL32(25, 25, 25, 100);
            dest.Colors[(int)ImNodesCol.MiniMapBackgroundHovered] = IM_COL32(25, 25, 25, 200);
            dest.Colors[(int)ImNodesCol.MiniMapOutline] = IM_COL32(150, 150, 150, 100);
            dest.Colors[(int)ImNodesCol.MiniMapOutlineHovered] = IM_COL32(150, 150, 150, 200);
            dest.Colors[(int)ImNodesCol.MiniMapNodeBackground] = IM_COL32(200, 200, 200, 100);
            dest.Colors[(int)ImNodesCol.MiniMapNodeBackgroundHovered] = IM_COL32(200, 200, 200, 255);
            dest.Colors[(int)ImNodesCol.MiniMapNodeBackgroundSelected] = dest.Colors[(int)ImNodesCol.MiniMapNodeBackgroundHovered];
            dest.Colors[(int)ImNodesCol.MiniMapNodeOutline] = IM_COL32(200, 200, 200, 100);
            dest.Colors[(int)ImNodesCol.MiniMapLink] = dest.Colors[(int)ImNodesCol.Link];
            dest.Colors[(int)ImNodesCol.MiniMapLinkSelected] = dest.Colors[(int)ImNodesCol.LinkSelected];
            dest.Colors[(int)ImNodesCol.MiniMapCanvas] = IM_COL32(200, 200, 200, 25);
            dest.Colors[(int)ImNodesCol.MiniMapCanvasOutline] = IM_COL32(200, 200, 200, 200);
        }

        /// <summary>
        /// Applies the light style preset.
        /// </summary>
        public static void StyleColorsLight(ImNodesStyle dest)
        {
            dest.Colors[(int)ImNodesCol.NodeBackground] = IM_COL32(240, 240, 240, 255);
            dest.Colors[(int)ImNodesCol.NodeBackgroundHovered] = IM_COL32(240, 240, 240, 255);
            dest.Colors[(int)ImNodesCol.NodeBackgroundSelected] = IM_COL32(240, 240, 240, 255);
            dest.Colors[(int)ImNodesCol.NodeOutline] = IM_COL32(100, 100, 100, 255);
            dest.Colors[(int)ImNodesCol.TitleBar] = IM_COL32(248, 248, 248, 255);
            dest.Colors[(int)ImNodesCol.TitleBarHovered] = IM_COL32(209, 209, 209, 255);
            dest.Colors[(int)ImNodesCol.TitleBarSelected] = IM_COL32(209, 209, 209, 255);
            dest.Colors[(int)ImNodesCol.Link] = IM_COL32(66, 150, 250, 100);
            dest.Colors[(int)ImNodesCol.LinkHovered] = IM_COL32(66, 150, 250, 242);
            dest.Colors[(int)ImNodesCol.LinkSelected] = IM_COL32(66, 150, 250, 242);
            dest.Colors[(int)ImNodesCol.Pin] = IM_COL32(66, 150, 250, 160);
            dest.Colors[(int)ImNodesCol.PinHovered] = IM_COL32(66, 150, 250, 255);
            dest.Colors[(int)ImNodesCol.BoxSelector] = IM_COL32(90, 170, 250, 30);
            dest.Colors[(int)ImNodesCol.BoxSelectorOutline] = IM_COL32(90, 170, 250, 150);
            dest.Colors[(int)ImNodesCol.GridBackground] = IM_COL32(225, 225, 225, 255);
            dest.Colors[(int)ImNodesCol.GridLine] = IM_COL32(180, 180, 180, 100);
            dest.Colors[(int)ImNodesCol.GridLinePrimary] = IM_COL32(120, 120, 120, 100);
            // Minimaps
            dest.Colors[(int)ImNodesCol.MiniMapBackground] = IM_COL32(25, 25, 25, 100);
            dest.Colors[(int)ImNodesCol.MiniMapBackgroundHovered] = IM_COL32(25, 25, 25, 200);
            dest.Colors[(int)ImNodesCol.MiniMapOutline] = IM_COL32(150, 150, 150, 100);
            dest.Colors[(int)ImNodesCol.MiniMapOutlineHovered] = IM_COL32(150, 150, 150, 200);
            dest.Colors[(int)ImNodesCol.MiniMapNodeBackground] = IM_COL32(200, 200, 200, 100);
            dest.Colors[(int)ImNodesCol.MiniMapNodeBackgroundHovered] = IM_COL32(200, 200, 240, 255);
            dest.Colors[(int)ImNodesCol.MiniMapNodeBackgroundSelected] = dest.Colors[(int)ImNodesCol.MiniMapNodeBackgroundHovered];
            dest.Colors[(int)ImNodesCol.MiniMapNodeOutline] = IM_COL32(200, 200, 200, 100);
            dest.Colors[(int)ImNodesCol.MiniMapLink] = dest.Colors[(int)ImNodesCol.Link];
            dest.Colors[(int)ImNodesCol.MiniMapLinkSelected] = dest.Colors[(int)ImNodesCol.LinkSelected];
            dest.Colors[(int)ImNodesCol.MiniMapCanvas] = IM_COL32(200, 200, 200, 25);
            dest.Colors[(int)ImNodesCol.MiniMapCanvasOutline] = IM_COL32(200, 200, 200, 200);
        }
    }
}
