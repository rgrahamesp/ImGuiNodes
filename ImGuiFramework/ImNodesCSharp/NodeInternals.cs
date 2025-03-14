using ImNodesCSharp.Math;
using Hexa.NET.ImGui;
using System;
using System.Numerics;
using System.Collections.Generic;

// In C++, ImNodesMiniMapNodeHoveringCallbackUserData is a void pointer. In C# you can simply use an object.
// You can define an alias if desired:
using ImNodesMiniMapNodeHoveringCallbackUserData = System.Object;
using System.Diagnostics;


namespace ImNodesCSharp
{
    /// <summary>
    /// Callback for when a mini-map node is hovered.
    /// Receives the node ID and an optional user data object.
    /// </summary>
    public delegate void ImNodesMiniMapNodeHoveringCallback(int nodeId, object userData);


    public enum ImNodesScope
    {
        None = 1,
        Editor = 1 << 1,
        Node = 1 << 2,
        Attribute = 1 << 3
    };

    public enum ImNodesAttributeType
    {
        None,
        Input,
        Output
    };

    public enum ImNodesUIState
    {
        None = 0,
        LinkStarted = 1 << 0,
        LinkDropped = 1 << 1,
        LinkCreated = 1 << 2
    };

    public enum ImNodesClickInteractionType
    {
        Node,
        Link,
        LinkCreation,
        Panning,
        BoxSelection,
        ImGuiItem,
        None
    };

    public enum ImNodesLinkCreationType
    {
        Standard,
        FromDetach
    };

    // The color indices are represented as an enum.
    public enum ImNodesCol : int
    {
        NodeBackground = 0,
        NodeBackgroundHovered,
        NodeBackgroundSelected,
        NodeOutline,
        TitleBar,
        TitleBarHovered,
        TitleBarSelected,
        Link,
        LinkHovered,
        LinkSelected,
        Pin,
        PinHovered,
        BoxSelector,
        BoxSelectorOutline,
        GridBackground,
        GridLine,
        GridLinePrimary,
        MiniMapBackground,
        MiniMapBackgroundHovered,
        MiniMapOutline,
        MiniMapOutlineHovered,
        MiniMapNodeBackground,
        MiniMapNodeBackgroundHovered,
        MiniMapNodeBackgroundSelected,
        MiniMapNodeOutline,
        MiniMapLink,
        MiniMapLinkSelected,
        MiniMapCanvas,
        MiniMapCanvasOutline,
        Count
    }

    public enum ImNodesStyleVar
    {
        GridSpacing = 0,
        NodeCornerRounding,
        NodePadding,
        NodeBorderThickness,
        LinkThickness,
        LinkLineSegmentsPerLength,
        LinkHoverDistance,
        PinCircleRadius,
        PinQuadSideLength,
        PinTriangleSideLength,
        PinLineThickness,
        PinHoverRadius,
        PinOffset,
        MiniMapPadding,
        MiniMapOffset,
        COUNT
    }

    [Flags]
    public enum ImNodesStyleFlags
    {
        None = 0,
        NodeOutline = 1 << 0,
        GridLines = 1 << 2,
        GridLinesPrimary = 1 << 3,
        GridSnapping = 1 << 4
    }


    [Flags]
    public enum ImNodesAttributeFlags
    {
        None = 0,
        EnableLinkDetachWithDragClick = 1 << 0,
        EnableLinkCreationOnSnap = 1 << 1
    }

    public enum ImNodesPinShape
    {
        Circle,
        CircleFilled,
        Triangle,
        TriangleFilled,
        Quad,
        QuadFilled
    };

    public enum ImNodesMiniMapLocation
    {
        BottomLeft,
        BottomRight,
        TopLeft,
        TopRight
    }
    public interface IHasId
    {
        int Id { get; set; }
    }

    public class ImObjectPool<T> where T : IHasId, new()
    {
        public List<T> Pool;
        public List<bool> InUse;
        public List<int> FreeList;
        public ImGuiStorage IdMap;

        //-------------------------------------------------------------------------
        public ImObjectPool()
        {
            Pool = new List<T>();
            InUse = new List<bool>();
            FreeList = new List<int>();
            IdMap = new ImGuiStorage();
        }

        /// <summary>
        /// Updates the pool by removing items that are marked as not in use.
        /// </summary>
        //-------------------------------------------------------------------------
        public void Update()
        {
            // Iterate backwards so that removal does not affect the indices of earlier elements.
            for (int i = Pool.Count - 1; i >= 0; i--)
            {
                if (!InUse[i])
                {
                    Pool.RemoveAt(i);
                    InUse.RemoveAt(i);
                }
            }
        }

        //-------------------------------------------------------------------------
        public void Clear()
        {
            Pool.Clear();
            InUse.Clear();
            FreeList.Clear();
            IdMap.Clear();
        }

        //-------------------------------------------------------------------------
        public int ObjectPoolFindOrCreateIndex(int id)
        {
            // Look for an object that is in use and has the given id.
            for (int i = 0; i < Pool.Count; i++)
            {
                if (InUse[i] && Pool[i].Id == id)
                    return i;
            }

            // Not found: create a new object.
            T newObj = new T();
            newObj.Id = id;
            Pool.Add(newObj);
            InUse.Add(true);
            return Pool.Count - 1;
        }

        //-------------------------------------------------------------------------
        public T ObjectPoolFindOrCreateObject(int id)
        {
            int index = ObjectPoolFindOrCreateIndex(id);
            return Pool[index];
        }

        //-------------------------------------------------------------------------
        public int ObjectPoolFind(int id)
        {
            int index = IdMap.GetInt((uint)id, -1);
            return index;
        }

        //-------------------------------------------------------------------------
        public bool IsObjectSelected(List<int> selectedIndices, int id)
        {
            int idx = ObjectPoolFind(id);
            return selectedIndices.Contains(idx);
        }

        //-------------------------------------------------------------------------
        public void SelectObject(List<int> selectedIndices, int id)
        {
            int idx = ObjectPoolFind(id);
            Debug.Assert(idx >= 0);
            Debug.Assert(!selectedIndices.Contains(idx));
            selectedIndices.Add(idx);
        }

        //-------------------------------------------------------------------------
        public void ClearObjectSelection(List<int> selectedIndices, int id)
        {
            int idx = ObjectPoolFind(id);
            Debug.Assert(idx >= 0);
            Debug.Assert(selectedIndices.Contains(idx));
            selectedIndices.RemoveAt(idx);
        }
    }

    public struct ImOptionalIndex : IEquatable<ImOptionalIndex>
    {
        public const int INVALID_INDEX = -1;
        private int _Index;

        // Default constructor: sets _Index to INVALID_INDEX.
        public ImOptionalIndex()
        {
            _Index = INVALID_INDEX;
        }

        // Constructor with a value.
        public ImOptionalIndex(int value)
        {
            _Index = value;
        }

        // Returns true if _Index is not INVALID_INDEX.
        public bool HasValue => _Index != INVALID_INDEX;

        // Returns the stored value; asserts if no value is present.
        public int Value
        {
            get
            {
                Debug.Assert(HasValue, "No value present in ImOptionalIndex");
                return _Index;
            }
        }

        // Resets the stored value to INVALID_INDEX.
        public void Reset() => _Index = INVALID_INDEX;

        // Implicit conversion from int to ImOptionalIndex.
        public static implicit operator ImOptionalIndex(int value)
        {
            return new ImOptionalIndex(value);
        }

        // Implicit conversion from ImOptionalIndex to int.
        public static implicit operator int(ImOptionalIndex opt)
        {
            return opt._Index;
        }

        public bool Equals(ImOptionalIndex other)
        {
            return _Index == other._Index;
        }

        public override bool Equals(object obj)
        {
            if (obj is ImOptionalIndex other)
                return Equals(other);
            if (obj is int i)
                return _Index == i;
            return false;
        }

        public override int GetHashCode()
        {
            return _Index.GetHashCode();
        }

        public static bool operator ==(ImOptionalIndex lhs, ImOptionalIndex rhs)
        {
            return lhs._Index == rhs._Index;
        }

        public static bool operator !=(ImOptionalIndex lhs, ImOptionalIndex rhs)
        {
            return lhs._Index != rhs._Index;
        }

        public static bool operator ==(ImOptionalIndex lhs, int rhs)
        {
            return lhs._Index == rhs;
        }

        public static bool operator !=(ImOptionalIndex lhs, int rhs)
        {
            return lhs._Index != rhs;
        }
    }

    // IO settings for ImNodes. Note: pointer fields are marked unsafe.
    public unsafe class ImNodesIO
    {
        public class EmulateThreeButtonMouse
        {
            public bool Modifier;
            public EmulateThreeButtonMouse() { Modifier = false; }
        }

        public class LinkDetachWithModifierClick
        {
            public bool Modifier;
            public LinkDetachWithModifierClick() { Modifier = false; }
        }

        public class MultipleSelectModifier
        {
            public bool Modifier;
            public MultipleSelectModifier() { Modifier = false; }
        }

        public EmulateThreeButtonMouse ThreeButtonMouse;
        public LinkDetachWithModifierClick LinkDetachModifierClick;
        public MultipleSelectModifier MultiSelectModifier;
        public ImGuiMouseButton AltMouseButton;
        public float AutoPanningSpeed;

        public ImNodesIO()
        {
            ThreeButtonMouse = new EmulateThreeButtonMouse();
            LinkDetachModifierClick = new LinkDetachWithModifierClick();
            MultiSelectModifier = new MultipleSelectModifier();
            AltMouseButton = ImGuiMouseButton.Right;
            AutoPanningSpeed = 500.0f; //1000.0f;
        }
    }

    // Style settings for ImNodes.
    public class ImNodesStyle
    {
        public float GridSpacing;
        public float NodeCornerRounding;
        public Vector2 NodePadding;
        public float NodeBorderThickness;
        public float LinkThickness;
        public float LinkLineSegmentsPerLength;
        public float LinkHoverDistance;
        public float PinCircleRadius;
        public float PinQuadSideLength;
        public float PinTriangleSideLength;
        public float PinLineThickness;
        public float PinHoverRadius;
        public float PinOffset;
        public Vector2 MiniMapPadding;
        public Vector2 MiniMapOffset;
        public ImNodesStyleFlags Flags;
        public uint[] Colors; // Array of colors indexed by ImNodesCol

        public ImNodesStyle()
        {
            GridSpacing = 24f;
            NodeCornerRounding = 4f;
            NodePadding = new Vector2(8f, 8f);
            NodeBorderThickness = 1f;
            LinkThickness = 3f;
            LinkLineSegmentsPerLength = 0.1f;
            LinkHoverDistance = 10f;
            PinCircleRadius = 4f;
            PinQuadSideLength = 7f;
            PinTriangleSideLength = 9.5f;
            PinLineThickness = 1f;
            PinHoverRadius = 10f;
            PinOffset = 0f;
            MiniMapPadding = new Vector2(8f, 8f);
            MiniMapOffset = new Vector2(4f, 4f);
            Flags = ImNodesStyleFlags.NodeOutline | ImNodesStyleFlags.GridLines;
            Colors = new uint[(int)ImNodesCol.Count];
        }
    }

    public class ImNodesColElement
    {
        public uint Color;
        public ImNodesCol Item;

        public ImNodesColElement(uint c, ImNodesCol s)
        {
            Color = c;
            Item = s;
        }
    };

    public class ImNodesStyleVarElement
    {
        public ImNodesStyleVar Item;
        public float[] FloatValue = new float[2];

        public ImNodesStyleVarElement(ImNodesStyleVar variable, float value)
        {
            Item = variable;
            FloatValue[0] = value;
        }

        public ImNodesStyleVarElement(ImNodesStyleVar variable, Vector2 value)
        {
            Item = variable;
            FloatValue[0] = value.X;
            FloatValue[1] = value.Y;
        }
    };

    public class LayoutStyle
    {
        public float CornerRounding = 0f;
        public Vector2 Padding = Vector2.Zero;
        public float BorderThickness = 1f;
    }

    public class NodeColorStyle
    {
        public uint Background = 0xFFFFFFFF;
        public uint BackgroundHovered;
        public uint BackgroundSelected;
        public uint Outline = 0x00FFFF00;
        public uint Titlebar;
        public uint TitlebarHovered;
        public uint TitlebarSelected;
    }

    public class ImNodeData : IHasId
    {
        public int Id { get; set; }
        public Vector2 Origin; // The node origin is in editor space
        public Rectangle TitleBarContentRect;
        public Rectangle Rect;

        public NodeColorStyle ColorStyle;
        public LayoutStyle LayoutStyle;

        public List<int> PinIndices;
        public bool Draggable;

        public ImNodeData() => Initialize(-1);

        public ImNodeData(int nodeId) => Initialize(nodeId);

        private void Initialize(int nodeId)
        {
            Id = nodeId;
            Origin = Vector2.Zero;
            TitleBarContentRect = new Rectangle();
            Rect = new Rectangle();
            ColorStyle = new NodeColorStyle();
            LayoutStyle = new LayoutStyle();
            PinIndices = new List<int>();
            Draggable = true;
        }
    }

    public class PinColorStyle
    {
        public uint Background;
        public uint Hovered;
    }
    
    public class ImPinData : IHasId
    {
        public int Id { get; set; }
        public int ParentNodeIdx;
        public Rectangle AttributeRect;
        public ImNodesAttributeType Type;
        public ImNodesPinShape Shape;
        public Vector2 Pos; // screen-space coordinates
        public ImNodesAttributeFlags Flags;
        public PinColorStyle ColorStyle;

        public ImPinData() => Initialize(-1);

        public ImPinData(int pinId) => Initialize(pinId);

        private void Initialize(int pinId)
        {
            Id = pinId;
            ParentNodeIdx = 0;
            AttributeRect = new Rectangle();
            Type = ImNodesAttributeType.None;
            Shape = ImNodesPinShape.CircleFilled;
            Pos = Vector2.Zero;
            Flags = ImNodesAttributeFlags.None;
            ColorStyle = new PinColorStyle();
        }
    }

    public class LinkColorStyle
    {
        public uint Base;
        public uint Hovered;
        public uint Selected;
    }

    public class ImLinkData : IHasId
    {
        public int Id { get; set; }
        public int StartPinIdx, EndPinIdx;
        public LinkColorStyle ColorStyle;

        public ImLinkData() => Initialize(0);

        public ImLinkData(int linkId) => Initialize(linkId);

        private void Initialize(int linkId)
        {
            Id = linkId;
            StartPinIdx = 0;
            EndPinIdx = 0;
            ColorStyle = new LinkColorStyle();
        }
    }

    public class LinkCreation
    {
        public ImOptionalIndex StartPinIdx = new ImOptionalIndex();
        public ImOptionalIndex EndPinIdx = new ImOptionalIndex();
        public ImNodesLinkCreationType Type = ImNodesLinkCreationType.Standard;
    }

    public class BoxSelector
    {
        public Rectangle Rect = new Rectangle(); // Coordinates in grid space
    }

    public class ImClickInteractionState
    {
        public ImNodesClickInteractionType Type;
        public LinkCreation LinkCreation;
        public BoxSelector BoxSelector;

        public ImClickInteractionState()
        {
            Type = ImNodesClickInteractionType.None;
            LinkCreation = new LinkCreation();
            BoxSelector = new BoxSelector();
        }
    }

    public class ImNodesEditorContext
    {
        public ImObjectPool<ImNodeData> Nodes;
        public ImObjectPool<ImPinData> Pins;
        public ImObjectPool<ImLinkData> Links;

        public List<int> NodeDepthOrder;

        // ui related fields
        public float ZoomScale;
        public Vector2 Panning;
        public Vector2 AutoPanningDelta;
        // Minimum and maximum extents of all content in grid space. Valid after final EndNode() call.
        public Rectangle GridContentBounds;

        public List<int> SelectedNodeIndices;
        public List<int> SelectedLinkIndices;

        // Relative origins of selected nodes for snapping of dragged nodes
        public List<Vector2> SelectedNodeOffsets = new List<Vector2>();
        // Offset of the primary node origin relative to the mouse cursor.
        public Vector2 PrimaryNodeOffset = Vector2.Zero;

        public ImClickInteractionState ClickInteraction;

        // Mini-map state set by MiniMap()

        public bool MiniMapEnabled;
        public ImNodesMiniMapLocation MiniMapLocation;
        public float MiniMapSizeFraction;
        public ImNodesMiniMapNodeHoveringCallback MiniMapNodeHoveringCallback;
        public ImNodesMiniMapNodeHoveringCallbackUserData MiniMapNodeHoveringCallbackUserData;

        // Mini-map state set during EndNodeEditor() call

        public Rectangle MiniMapRectScreenSpace;
        public Rectangle MiniMapContentScreenSpace;
        public float MiniMapScaling;

        public ImNodesEditorContext()
        {
            Nodes = new ImObjectPool<ImNodeData>();
            Pins = new ImObjectPool<ImPinData>();
            Links = new ImObjectPool<ImLinkData>();
            NodeDepthOrder = new List<int>();
            ZoomScale = 1.0f;
            Panning = Vector2.Zero;
            AutoPanningDelta = Vector2.Zero;
            GridContentBounds = new Rectangle();
            SelectedNodeIndices = new List<int>();
            SelectedLinkIndices = new List<int>();
            SelectedNodeOffsets = new List<Vector2>();
            PrimaryNodeOffset = Vector2.Zero;
            ClickInteraction = new ImClickInteractionState();
            MiniMapEnabled = false;
            MiniMapLocation = ImNodesMiniMapLocation.TopLeft;
            MiniMapSizeFraction = 0.0f;
            MiniMapNodeHoveringCallback = null;
            MiniMapNodeHoveringCallbackUserData = null;
            MiniMapRectScreenSpace = new Rectangle();
            MiniMapContentScreenSpace = new Rectangle();
            MiniMapScaling = 0.0f;
        }
    }

    public class ImNodesContext
    {
        public ImNodesEditorContext EditorCtx = null;

        // Canvas draw list and helper state
        public ImDrawListPtr CanvasDrawList = null;
        public ImGuiStorage NodeIdxToSubmissionIdx = new ImGuiStorage();
        public List<int> NodeIdxSubmissionOrder = new List<int>();
        public List<int> NodeIndicesOverlappingWithMouse = new List<int>();
        public List<int> OccludedPinIndices = new List<int>();

        // Canvas extents
        public Vector2 CanvasOriginalOrigin = Vector2.Zero;
        public Vector2 CanvasOriginScreenSpace = Vector2.Zero;
        public Rectangle CanvasRectScreenSpace = new Rectangle();

        // Debug helpers
        public ImNodesScope CurrentScope = ImNodesScope.None;

        // Configuration state
        public ImNodesIO Io = new ImNodesIO();
        public ImNodesStyle Style = new ImNodesStyle();
        public List<ImNodesColElement> ColorModifierStack = new List<ImNodesColElement>();
        public List<ImNodesStyleVarElement> StyleModifierStack = new List<ImNodesStyleVarElement>();
        public ImGuiTextBufferPtr TextBuffer = new ImGuiTextBufferPtr();

        public ImNodesAttributeFlags CurrentAttributeFlags = ImNodesAttributeFlags.None;
        public List<ImNodesAttributeFlags> AttributeFlagStack = new List<ImNodesAttributeFlags>();

        // UI element state
        public int CurrentNodeIdx = -1;
        public int CurrentPinIdx = -1;
        public int CurrentAttributeId = -1;

        public ImOptionalIndex HoveredNodeIdx = new ImOptionalIndex();
        public ImOptionalIndex HoveredLinkIdx = new ImOptionalIndex();
        public ImOptionalIndex HoveredPinIdx = new ImOptionalIndex();

        public ImOptionalIndex DeletedLinkIdx = new ImOptionalIndex();
        public ImOptionalIndex SnapLinkIdx = new ImOptionalIndex();

        // Event helper state
        // TODO: this should be a part of a state machine, and not a member of the global struct.
        // Unclear what parts of the code this relates to.
        public ImNodesUIState ImNodesUIState = ImNodesUIState.None;

        public int ActiveAttributeId = -1;
        public bool ActiveAttribute = false;

        // ImGui::IO cache

        public Vector2 MousePos = Vector2.Zero;
        public bool IsHovered = false;

        public bool LeftMouseClicked;
        public bool LeftMouseReleased;
        public bool AltMouseClicked;
        public bool LeftMouseDragging;
        public bool AltMouseDragging;
        public float AltMouseScrollDelta;
        public bool MultipleSelectModifier;
    };
}
