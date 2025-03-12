namespace ImNodesCSharp
{
    using Hexa.NET.ImGui;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Numerics;
    using System;
    using ImNodesCSharp.Math;
    using ImDrawIdx = UInt16;
    using System.Xml.Linq;

    public static unsafe class ImNodes
    {
        public static ImNodesContext ImNodesCtx { get; private set; }

        public static int GetUniqueId() => _nextUniqueId++;
        private static int _nextUniqueId = 0;

        //-------------------------------------------------------------------------
        public static ImNodesContext CreateContext()
        {
            // Create a new context instance.
            ImNodesContext ctx = new ImNodesContext();

            // If no context is currently set, make this the current context.
            if (ImNodesCtx == null)
                SetCurrentContext(ctx);

            // Perform any additional initialization.
            Initialize(ctx);

            return ctx;
        }

        //-------------------------------------------------------------------------
        public static void SetCurrentContext(ImNodesContext ctx)
        {
            ImNodesCtx = ctx;
        }

        //-------------------------------------------------------------------------
        private static void Initialize(ImNodesContext ctx)
        {
            ctx.NodeEditorImgCtx = ImGui.CreateContext(ImGui.GetIO().Fonts);
            ctx.NodeEditorImgCtx.IO.IniFilename = null;
            ctx.OriginalImgCtx = null;

            ctx.CanvasOriginalOrigin = new Vector2(0.0f, 0.0f);
            ctx.CanvasOriginScreenSpace = new Vector2(0.0f, 0.0f);
            ctx.CanvasRectScreenSpace = new Rectangle(Vector2.Zero, Vector2.Zero);
            ctx.CurrentScope = ImNodesScope.None;

            ctx.CurrentPinIdx = int.MaxValue;
            ctx.CurrentNodeIdx = int.MaxValue;

            ctx.DefaultEditorCtx = new ImNodesEditorContext();
            ctx.EditorCtx = ctx.DefaultEditorCtx;

            ctx.CurrentAttributeFlags = ImNodesAttributeFlags.None;
            ctx.AttributeFlagStack.Add(ImNodesCtx.CurrentAttributeFlags);

            SetNodeGridSpacePos(1, new Vector2(200.0f, 200.0f));

            NodeStylePresets.StyleColorsDark(ctx.Style);
        }

        //-------------------------------------------------------------------------
        public static void DestroyContext(ImNodesContext ctx = null)
        {
            // If no context is passed, destroy the current one.
            if (ctx == null)
                ctx = ImNodesCtx;

            // Perform any shutdown/cleanup steps.
            Shutdown(ctx);

            // If the global context is being destroyed, clear it.
            if (ImNodesCtx == ctx)
                SetCurrentContext(null);

            // In C#, garbage collection will reclaim the memory.
            // If ImNodesContext implements IDisposable, you could call ctx.Dispose() here.
        }

        //-------------------------------------------------------------------------
        private static unsafe void Shutdown(ImNodesContext ctx)
        {
            ImGui.DestroyContext(ctx.NodeEditorImgCtx);
        }

        public static void SetImGuiContext(ImGuiContextPtr ctx) { ImGui.SetCurrentContext(ctx); }
        public static ImNodesEditorContext EditorContextGet() => ImNodesCtx.EditorCtx;
        public static void EditorContextSet(ImNodesEditorContext ctx) { ImNodesCtx.EditorCtx = ctx; }
        private static void DrawListSet(ImDrawListPtr drawList) => ImNodesCtx.CanvasDrawList = new ImDrawListPtr(drawList);
        public static bool MouseInCanvas() => ImNodesCtx.IsHovered;
        private static bool IsMiniMapActive() => EditorContextGet().MiniMapEnabled && EditorContextGet().MiniMapSizeFraction > 0.0f;
        public static float EditorContextGetZoom() => EditorContextGet().ZoomScale;

        //-------------------------------------------------------------------------
        public static void EditorContextSetZoom(float zoom_scale, Vector2 zoom_centering_pos)
        {
            ImNodesEditorContext editor = EditorContextGet();
            float new_zoom = System.Math.Max(0.1f, System.Math.Min(10.0f, zoom_scale));

            zoom_centering_pos -= ImNodesCtx.CanvasOriginalOrigin;
            editor.Panning += zoom_centering_pos / new_zoom - zoom_centering_pos / editor.ZoomScale;

            // Fix mouse position
            ImGui.GetIO().MousePos *= editor.ZoomScale / new_zoom;

            editor.ZoomScale = new_zoom;
        }

        //-------------------------------------------------------------------------
        public static void BeginNodeTitleBar()
        {
            // Ensure we are in a Node scope.
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.Node, "BeginNodeTitleBar must be called in Node scope");
            // Start a new ImGui group to hold the title bar content.
            ImGui.BeginGroup();
        }

        //-------------------------------------------------------------------------
        public static void EndNodeTitleBar()
        {
            // Ensure we are in Node scope.
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.Node, "EndNodeTitleBar must be called in Node scope");

            // End the ImGui group started in BeginNodeTitleBar.
            ImGui.EndGroup();

            // Retrieve the current editor context.
            ImNodesEditorContext editor = EditorContextGet();
            // Get the node data for the current node.
            var node = editor.Nodes.Pool[ImNodesCtx.CurrentNodeIdx];

            // Capture the rectangle occupied by the title bar content.
            node.TitleBarContentRect = GetItemRect();

            // Register the title bar area with ImGui for hit-testing.
            ImGuiP.ItemAdd(GetNodeTitleRect(node).ToImRect(), ImGui.GetID("title_bar"));

            // Set the cursor position for the node's content.
            ImGui.SetCursorPos(GridSpaceToEditorSpace(editor, GetNodeContentOrigin(node)));
        }

        //-------------------------------------------------------------------------
        public static void BeginNodeEditor()
        {
            // Make sure that no editor is already active.
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.None);
            ImNodesCtx.CurrentScope = ImNodesScope.Editor;

            // Get the current editor context (this function would be part of your wrapper).
            ImNodesEditorContext editor = EditorContextGet();
            editor.AutoPanningDelta = Vector2.Zero;
            editor.GridContentBounds = new Rectangle(float.MaxValue, float.MaxValue, -float.MaxValue, -float.MaxValue);
            editor.MiniMapEnabled = false;
            editor.Nodes.Clear();
            editor.Pins.Clear();
            editor.Links.Clear();

            // Reset hovered state.
            ImNodesCtx.HoveredNodeIdx.Reset();
            ImNodesCtx.HoveredLinkIdx.Reset();
            ImNodesCtx.HoveredPinIdx.Reset();
            ImNodesCtx.DeletedLinkIdx.Reset();
            ImNodesCtx.SnapLinkIdx.Reset();
            
            ImNodesCtx.NodeIndicesOverlappingWithMouse.Clear();

            ImNodesCtx.ImNodesUIState = (int)ImNodesUIState.None;

            ImNodesCtx.ActiveAttribute = false;
            ImNodesCtx.IsHovered = false;

            // Begin an ImGui group (assuming your binding exposes these functions)
            ImGui.BeginGroup();
            // Setup zoom context and switch to a dedicated node editor context.
            Vector2 canvasSize = ImGui.GetContentRegionAvail();
            ImNodesCtx.CanvasOriginalOrigin = ImGui.GetCursorScreenPos();
            ImNodesCtx.OriginalImgCtx = ImGui.GetCurrentContext();

            // Copy IO settings from the main context to the node editor context.
            ImNodesCtx.NodeEditorImgCtx.IO = ImNodesCtx.OriginalImgCtx.IO;
            ImNodesCtx.NodeEditorImgCtx.IO.BackendPlatformUserData = null;
            ImNodesCtx.NodeEditorImgCtx.IO.BackendRendererUserData = null;
            ImNodesCtx.NodeEditorImgCtx.IO.IniFilename = null;
            ImNodesCtx.NodeEditorImgCtx.IO.ConfigInputTrickleEventQueue = 0;
            ImNodesCtx.NodeEditorImgCtx.IO.DisplaySize = Vector2.Max(canvasSize / editor.ZoomScale, Vector2.Zero);
            ImNodesCtx.NodeEditorImgCtx.Style = ImNodesCtx.OriginalImgCtx.Style;

            // Set window flags so that the node editor window does not allow moving or scrolling.
            ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoDecoration |
                                             ImGuiWindowFlags.NoSavedSettings |
                                             ImGuiWindowFlags.NoScrollWithMouse |
                                             ImGuiWindowFlags.NoMove |
                                             ImGuiWindowFlags.NoBackground; // FIXME from https://github.com/Nelarius/imnodes/pull/192

            if (ImGui.IsWindowHovered())
                ImNodesCtx.IsHovered = true;
            else
            {
                windowFlags |= ImGuiWindowFlags.NoInputs;
                ImNodesCtx.NodeEditorImgCtx.IO.ConfigFlags |= ImGuiConfigFlags.NoMouse;
            }

            // Copy input events from the original context to the node editor context.
            ImNodesCtx.NodeEditorImgCtx.InputEventsQueue = new ImVector<ImGuiInputEvent>();
            for (int i = 0; i < ImNodesCtx.OriginalImgCtx.InputEventsTrail.Size; i++)
                ImNodesCtx.NodeEditorImgCtx.InputEventsQueue.PushBack(ImNodesCtx.OriginalImgCtx.InputEventsTrail[i]);

            for (int i = 0; i < ImNodesCtx.NodeEditorImgCtx.InputEventsQueue.Size; i++)
            {
                var e = ImNodesCtx.NodeEditorImgCtx.InputEventsQueue[i];
                if (e.Type == ImGuiInputEventType.MousePos)
                {
                    e.Union.MousePos.PosX = (e.Union.MousePos.PosX - ImNodesCtx.CanvasOriginalOrigin.X) / editor.ZoomScale;
                    e.Union.MousePos.PosY = (e.Union.MousePos.PosY - ImNodesCtx.CanvasOriginalOrigin.Y) / editor.ZoomScale;
                }
            }

            ImGui.SetCurrentContext(ImNodesCtx.NodeEditorImgCtx);
            ImGui.NewFrame();

            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(1, 1));
            ImGui.PushStyleColor(ImGuiCol.WindowBg, ImNodesCtx.Style.Colors[(int)ImNodesCol.GridBackground]);
            ImGui.Begin("editor_canvas", null, windowFlags);
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor();

            ImNodesCtx.CanvasOriginScreenSpace = ImGui.GetCursorScreenPos();

            // Set the draw list for the node editor.
            DrawListSet(ImGui.GetWindowDrawList());

            Vector2 windowSize = ImGui.GetWindowSize();
            ImNodesCtx.CanvasRectScreenSpace = new Rectangle(
                EditorSpaceToScreenSpace(new Vector2(0, 0)),
                EditorSpaceToScreenSpace(windowSize));

            // Optionally draw grid lines if enabled.
            if ((ImNodesCtx.Style.Flags & ImNodesStyleFlags.GridLines) != 0)
                DrawGrid(editor, windowSize);

            // Cache input state
            ImNodesCtx.MousePos = ImGui.GetIO().MousePos;
            ImNodesCtx.LeftMouseClicked = ImGuiP.IsMouseClicked(ImGuiMouseButton.Left);
            ImNodesCtx.LeftMouseReleased = ImGuiP.IsMouseReleased(ImGuiMouseButton.Left);
            ImNodesCtx.LeftMouseDragging = ImGui.IsMouseDragging(ImGuiMouseButton.Left, 0.0f);
            ImNodesCtx.AltMouseClicked = (ImNodesCtx.Io.ThreeButtonMouse.Modifier && ImNodesCtx.LeftMouseClicked) || ImGuiP.IsMouseClicked(ImNodesCtx.Io.AltMouseButton);
            ImNodesCtx.AltMouseDragging = (ImNodesCtx.LeftMouseDragging && ImNodesCtx.Io.ThreeButtonMouse.Modifier) || ImGui.IsMouseDragging(ImNodesCtx.Io.AltMouseButton, 0.0f);
            ImNodesCtx.AltMouseScrollDelta = ImGui.GetIO().MouseWheel;
            ImNodesCtx.MultipleSelectModifier = (ImNodesCtx.Io.MultiSelectModifier.Modifier || ImGui.GetIO().KeyCtrl);
        }

        //-------------------------------------------------------------------------
        public static void EndNodeEditor()
        {
            // Ensure we are in the editor scope.
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.Editor);
            ImNodesCtx.CurrentScope = ImNodesScope.None;

            // Get the current editor context.
            var editor = EditorContextGet();

            // If grid content bounds are inverted, recalc from the canvas.
            if (editor.GridContentBounds.IsInverted())
            {
                editor.GridContentBounds = ScreenSpaceToGridSpace(editor, ImNodesCtx.CanvasRectScreenSpace);
            }

            // Detect if any ImGui widget is active; if so, mark click interaction accordingly.
            if (ImNodesCtx.LeftMouseClicked && ImGui.IsAnyItemActive())
            {
                editor.ClickInteraction.Type = ImNodesClickInteractionType.ImGuiItem;
            }

            // If no UI interaction is already in progress (or a link is being created),
            // and the canvas is hovered (but not the mini-map), determine what element is hovered.
            if ((editor.ClickInteraction.Type == ImNodesClickInteractionType.None ||
                 editor.ClickInteraction.Type == ImNodesClickInteractionType.LinkCreation) &&
                MouseInCanvas() && !IsMiniMapHovered())
            {
                // Resolve pins occluded by nodes.
                ResolveOccludedPins(editor, ImNodesCtx.OccludedPinIndices);
                ImNodesCtx.HoveredPinIdx = ResolveHoveredPin(editor.Pins, ImNodesCtx.OccludedPinIndices);

                // If no pin is hovered, check for nodes.
                if (!ImNodesCtx.HoveredPinIdx.HasValue)
                {
                    ImNodesCtx.HoveredNodeIdx = ResolveHoveredNode(editor.NodeDepthOrder);
                }

                // If no node is hovered, then check for links.
                if (!ImNodesCtx.HoveredNodeIdx.HasValue)
                {
                    ImNodesCtx.HoveredLinkIdx = ResolveHoveredLink(editor.Links, editor.Pins);
                }
            }

            // Render each node.
            for (int nodeIdx = 0; nodeIdx < editor.Nodes.Pool.Count; nodeIdx++)
            {
                if (editor.Nodes.InUse[nodeIdx])
                {
                    DrawListActivateNodeBackground(nodeIdx);
                    DrawNode(editor, nodeIdx);
                }
            }

            // Set the draw channel for links (channel 0 is the base channel).
            ImNodesCtx.CanvasDrawList.ChannelsSetCurrent(0);

            // Render each link.
            for (int linkIdx = 0; linkIdx < editor.Links.Pool.Count; linkIdx++)
            {
                if (editor.Links.InUse[linkIdx])
                {
                    DrawLink(editor, linkIdx);
                }
            }

            // Render UI elements for click interaction (e.g. partial links, box selection).
            DrawListAppendClickInteractionChannel();
            DrawListActivateClickInteractionChannel();

            // If the mini-map is active, update its layout and draw it.
            if (IsMiniMapActive())
            {
                CalcMiniMapLayout();
                MiniMapUpdate();
            }

            // Handle interactions with the node graph if not interacting with the mini-map.
            if (!IsMiniMapHovered())
            {
                if (ImNodesCtx.LeftMouseClicked && ImNodesCtx.HoveredLinkIdx.HasValue)
                {
                    BeginLinkInteraction(editor, ImNodesCtx.HoveredLinkIdx.Value, ImNodesCtx.HoveredPinIdx);
                }
                else if (ImNodesCtx.LeftMouseClicked && ImNodesCtx.HoveredPinIdx.HasValue)
                {
                    BeginLinkCreation(editor, ImNodesCtx.HoveredPinIdx.Value);
                }
                else if (ImNodesCtx.LeftMouseClicked && ImNodesCtx.HoveredNodeIdx.HasValue)
                {
                    BeginNodeSelection(editor, ImNodesCtx.HoveredNodeIdx.Value);
                }
                else if (ImNodesCtx.LeftMouseClicked || ImNodesCtx.LeftMouseReleased ||
                         ImNodesCtx.AltMouseClicked || ImNodesCtx.AltMouseScrollDelta != 0f)
                {
                    BeginCanvasInteraction(editor);
                }

                bool shouldAutoPan = (editor.ClickInteraction.Type == ImNodesClickInteractionType.BoxSelection ||
                                      editor.ClickInteraction.Type == ImNodesClickInteractionType.LinkCreation ||
                                      editor.ClickInteraction.Type == ImNodesClickInteractionType.Node);
                if (shouldAutoPan && !MouseInCanvas())
                {
                    Vector2 mouse = ImGui.GetMousePos();
                    Vector2 center = ImNodesCtx.CanvasRectScreenSpace.GetCenter();
                    Vector2 direction = center - mouse;
                    direction *= ImInvLength(direction, 0.0f);

                    editor.AutoPanningDelta = direction * ImGui.GetIO().DeltaTime * ImNodesCtx.Io.AutoPanningSpeed;
                    editor.Panning += editor.AutoPanningDelta;
                }
            }

            // Process click interaction updates.
            ClickInteractionUpdate(editor);

            // Update object pools for nodes and pins.
            editor.Nodes.Update();
            editor.Pins.Update();

            // Sort the draw channels by node depth.
            DrawListSortChannelsByDepth(editor.NodeDepthOrder);

            // Update the link pool.
            editor.Links.Update();

            // Merge the draw channels.
            ImNodesCtx.CanvasDrawList.ChannelsMerge();

            // Update the text input flag on the original ImGui context.
            ImNodesCtx.OriginalImgCtx.WantTextInputNextFrame =
                System.Math.Max(ImNodesCtx.OriginalImgCtx.WantTextInputNextFrame, ImNodesCtx.NodeEditorImgCtx.WantTextInputNextFrame);

            if (MouseInCanvas())
            {
                ImNodesCtx.OriginalImgCtx.MouseCursor = ImNodesCtx.NodeEditorImgCtx.MouseCursor;
            }

            // End the node editor frame.
            ImGui.End();
            ImGui.Render();

            var drawData = ImGui.GetDrawData();

            // Restore the original ImGui context.
            ImGui.SetCurrentContext(ImNodesCtx.OriginalImgCtx);
            ImNodesCtx.OriginalImgCtx = null;

            ImGui.EndChild();
            ImGui.EndGroup();

            // Copy the draw data from the node editor context to the original context.
            if (!drawData.IsNull)
            {
                for (int i = 0; i < drawData.CmdListsCount; i++)
                {
                    AppendDrawData(drawData.CmdLists[i], ImNodesCtx.CanvasOriginalOrigin, editor.ZoomScale);
                }
            }
        }

        //-------------------------------------------------------------------------
        public static unsafe void AppendDrawData(ImDrawListPtr src, Vector2 origin, float scale)
        {
            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            int vtx_start = dl.VtxBuffer.Size;
            int idx_start = dl.IdxBuffer.Size;
            dl.VtxBuffer.Resize(dl.VtxBuffer.Size + src.VtxBuffer.Size);
            dl.IdxBuffer.Resize(dl.IdxBuffer.Size + src.IdxBuffer.Size);
            dl.CmdBuffer.Resize(dl.CmdBuffer.Size + src.CmdBuffer.Size);
            dl.VtxWritePtr = dl.VtxBuffer.Data + vtx_start;
            dl.IdxWritePtr = dl.IdxBuffer.Data + idx_start;
            ImDrawVert* vtx_read = src.VtxBuffer.Data;
            ImDrawIdx* idx_read = src.IdxBuffer.Data;
            for (int i = 0, c = src.VtxBuffer.Size; i < c; ++i)
            {
                dl.VtxWritePtr.Handle[i].Uv = vtx_read[i].Uv;
                dl.VtxWritePtr.Handle[i].Col = vtx_read[i].Col;
                dl.VtxWritePtr.Handle[i].Pos = vtx_read[i].Pos * scale + origin;
            }
            for (int i = 0, c = src.IdxBuffer.Size; i < c; ++i)
            {
                dl.IdxWritePtr[i] = (ushort)(idx_read[i] + (ImDrawIdx)vtx_start);
            }
            for (int i = 0, c = src.CmdBuffer.Size; i < c; ++i)
            {
                ImDrawCmd cmd = src.CmdBuffer[i];
                cmd.IdxOffset += (uint)idx_start;
                cmd.ClipRect.X = cmd.ClipRect.X * scale + origin.X;
                cmd.ClipRect.Y = cmd.ClipRect.Y * scale + origin.Y;
                cmd.ClipRect.Z = cmd.ClipRect.Z * scale + origin.X;
                cmd.ClipRect.W = cmd.ClipRect.W * scale + origin.Y;
                dl.CmdBuffer.PushBack(cmd);
            }

            dl.VtxCurrentIdx += (uint)src.VtxBuffer.Size;
            dl.VtxWritePtr = dl.VtxBuffer.Data + dl.VtxBuffer.Size;
            dl.IdxWritePtr = dl.IdxBuffer.Data + dl.IdxBuffer.Size;
        }

        //-------------------------------------------------------------------------
        public static void BeginNode(int nodeId)
        {
            // Ensure that BeginNodeEditor() was already called.
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.Editor, "BeginNode must be called within an editor scope.");
            ImNodesCtx.CurrentScope = ImNodesScope.Node;

            // Get the current editor context.
            ImNodesEditorContext editor = EditorContextGet();

            // Retrieve (or create) the node index in the object pool.
            int nodeIdx = editor.Nodes.ObjectPoolFindOrCreateIndex(nodeId);
            ImNodesCtx.CurrentNodeIdx = nodeIdx;

            // Get the node data and initialize its style properties from the global style.
            var node = editor.Nodes.Pool[nodeIdx];
            node.ColorStyle.Background = ImNodesCtx.Style.Colors[(int)ImNodesCol.NodeBackground];
            node.ColorStyle.BackgroundHovered = ImNodesCtx.Style.Colors[(int)ImNodesCol.NodeBackgroundHovered];
            node.ColorStyle.BackgroundSelected = ImNodesCtx.Style.Colors[(int)ImNodesCol.NodeBackgroundSelected];
            node.ColorStyle.Outline = ImNodesCtx.Style.Colors[(int)ImNodesCol.NodeOutline];
            node.ColorStyle.Titlebar = ImNodesCtx.Style.Colors[(int)ImNodesCol.TitleBar];
            node.ColorStyle.TitlebarHovered = ImNodesCtx.Style.Colors[(int)ImNodesCol.TitleBarHovered];
            node.ColorStyle.TitlebarSelected = ImNodesCtx.Style.Colors[(int)ImNodesCol.TitleBarSelected];

            node.LayoutStyle.CornerRounding = ImNodesCtx.Style.NodeCornerRounding;
            node.LayoutStyle.Padding = ImNodesCtx.Style.NodePadding;
            node.LayoutStyle.BorderThickness = ImNodesCtx.Style.NodeBorderThickness;

            // Set the ImGui cursor position so that the node title bar begins at the correct editor space position.
            // Here, GridSpaceToEditorSpace converts a position from grid space to editor space,
            // and GetNodeTitleBarOrigin computes the starting point for the title bar.
            ImGui.SetCursorPos(GridSpaceToEditorSpace(editor, GetNodeTitleBarOrigin(node)));

            // Register the node in the draw list (which may add draw channels, etc.)
            DrawListAddNode(nodeIdx);
            DrawListActivateCurrentNodeForeground();

            // Push a unique ID for this node and begin an ImGui group to contain the node's content.
            ImGui.PushID(node.Id);
            ImGui.BeginGroup();
        }

        //-------------------------------------------------------------------------
        public static void DrawListAddNode(int nodeIdx)
        {
            // Map the node index to the current submission order index.
            // (Assume SetInt takes an integer key and a value, e.g. for a dictionary-like structure.)
            ImNodesCtx.NodeIdxToSubmissionIdx.SetInt((uint)nodeIdx, ImNodesCtx.NodeIdxSubmissionOrder.Count);

            // Add the node index to the submission order list.
            ImNodesCtx.NodeIdxSubmissionOrder.Add(nodeIdx);

            // Grow the draw list channels by 2 so that this node gets its background and foreground channels.
            ImDrawListGrowChannels(ImNodesCtx.CanvasDrawList, 2);
        }

        //-------------------------------------------------------------------------
        public static int DrawListSubmissionIdxToBackgroundChannelIdx(int submission_idx)
        {
            return 1 + 2 * submission_idx; // NOTE: the first channel is the canvas background, i.e. the grid
        }

        //-------------------------------------------------------------------------
        public static int DrawListSubmissionIdxToForegroundChannelIdx(int submission_idx)
        {
            return DrawListSubmissionIdxToBackgroundChannelIdx(submission_idx) + 1;
        }

        //-------------------------------------------------------------------------
        public static void DrawListActivateCurrentNodeForeground()
        {
            // Get the submission index for the current node (which is the last element in the submission order).
            int submissionIdx = ImNodesCtx.NodeIdxSubmissionOrder.Count - 1;

            // Convert the submission index to a foreground channel index.
            int foregroundChannelIdx = DrawListSubmissionIdxToForegroundChannelIdx(submissionIdx);

            // Activate the corresponding draw channel in the canvas draw list splitter.
            ImNodesCtx.CanvasDrawList.Splitter.SetCurrentChannel(ImNodesCtx.CanvasDrawList, 2);// foregroundChannelIdx); // 2);
        }

        //-------------------------------------------------------------------------
        public static void DrawListActivateNodeBackground(int nodeIdx)
        {
            // Assume GetInt returns the submission index (or -1 if not found).
            int submissionIdx = ImNodesCtx.NodeIdxToSubmissionIdx.GetInt((uint)nodeIdx, -1);
            Debug.Assert(submissionIdx != -1, "Invalid submission index");
            int backgroundChannelIdx = DrawListSubmissionIdxToBackgroundChannelIdx(submissionIdx);
            // Activate the channel in the draw list splitter.
            ImNodesCtx.CanvasDrawList.Splitter.SetCurrentChannel(ImNodesCtx.CanvasDrawList, 1);// backgroundChannelIdx); // 1);
        }

        //-------------------------------------------------------------------------
        public static void EndNode()
        {
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.Node, "EndNode() must be called while in Node scope.");
            ImNodesCtx.CurrentScope = ImNodesScope.Editor;

            ImNodesEditorContext editor = EditorContextGet();

            // End the ImGui group that was begun in BeginNode, and pop the unique ID.
            ImGui.EndGroup();
            ImGui.PopID();

            // Update the node's rectangle based on the current ImGui group.
            var node = editor.Nodes.Pool[ImNodesCtx.CurrentNodeIdx];
            node.Rect = GetItemRect(); // Assumes GetItemRect() returns a Rectangle corresponding to the current ImGui group.
            node.Rect.Expand(node.LayoutStyle.Padding);

            // Expand the editor's grid content bounds based on the node's origin and size.
            editor.GridContentBounds.Add(node.Origin);
            editor.GridContentBounds.Add(node.Origin + node.Rect.GetSize());

            // If the node's rectangle contains the current mouse position, mark this node as overlapping.
            if (node.Rect.Contains(ImNodesCtx.MousePos))
            {
                ImNodesCtx.NodeIndicesOverlappingWithMouse.Add(ImNodesCtx.CurrentNodeIdx);
            }
        }

        //-------------------------------------------------------------------------
        public static void BeginInputAttribute(int id, ImNodesPinShape shape = ImNodesPinShape.CircleFilled)
        {
            BeginPinAttribute(id, ImNodesAttributeType.Input, shape, ImNodesCtx.CurrentNodeIdx);
        }

        //-------------------------------------------------------------------------
        public static void EndInputAttribute()
        {
            EndPinAttribute();
        }

        //-------------------------------------------------------------------------
        public static void BeginOutputAttribute(int id, ImNodesPinShape shape = ImNodesPinShape.CircleFilled)
        {
            BeginPinAttribute(id, ImNodesAttributeType.Output, shape, ImNodesCtx.CurrentNodeIdx);
        }

        //-------------------------------------------------------------------------
        public static void EndOutputAttribute()
        {
            EndPinAttribute();
        }

        //-------------------------------------------------------------------------
        public static void BeginPinAttribute(int id, ImNodesAttributeType type, ImNodesPinShape shape, int nodeIdx)
        {
            // Ensure that we are in Node scope before starting an attribute.
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.Node, "BeginPinAttribute must be called from within a node");
            ImNodesCtx.CurrentScope = ImNodesScope.Attribute;

            // Begin an ImGui group for the attribute and push a unique ID.
            ImGui.BeginGroup();
            ImGui.PushID(id);

            // Get the current editor context.
            ImNodesEditorContext editor = EditorContextGet();

            // Set the current attribute ID.
            ImNodesCtx.CurrentAttributeId = id;

            // Retrieve (or create) the pin index for this attribute.
            int pinIdx = editor.Pins.ObjectPoolFindOrCreateIndex(id);
            ImNodesCtx.CurrentPinIdx = pinIdx;

            // Initialize the pin data.
            var pin = editor.Pins.Pool[pinIdx];
            pin.Id = id;
            pin.ParentNodeIdx = nodeIdx;
            pin.Type = type;
            pin.Shape = shape;
            pin.Flags = ImNodesCtx.CurrentAttributeFlags;
            pin.ColorStyle.Background = ImNodesCtx.Style.Colors[(int)ImNodesCol.Pin];
            pin.ColorStyle.Hovered = ImNodesCtx.Style.Colors[(int)ImNodesCol.PinHovered];
        }

        //-------------------------------------------------------------------------
        public static void EndPinAttribute()
        {
            // Ensure we are in attribute scope.
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.Attribute, "EndPinAttribute must be called while in Attribute scope");
            // Restore scope to Node.
            ImNodesCtx.CurrentScope = ImNodesScope.Node;

            // Pop the ID and end the attribute group.
            ImGui.PopID();
            ImGui.EndGroup();

            // If the current item (the attribute) is active, mark it.
            if (ImGui.IsItemActive())
            {
                ImNodesCtx.ActiveAttribute = true;
                ImNodesCtx.ActiveAttributeId = ImNodesCtx.CurrentAttributeId;
            }

            // Get the current editor context.
            ImNodesEditorContext editor = EditorContextGet();

            // Capture the attribute rectangle (from the current ImGui item).
            var pin = editor.Pins.Pool[ImNodesCtx.CurrentPinIdx];
            pin.AttributeRect = GetItemRect();

            // Add this pin's index to its parent node's list of pins.
            editor.Nodes.Pool[ImNodesCtx.CurrentNodeIdx].PinIndices.Add(ImNodesCtx.CurrentPinIdx);
        }

        // [SECTION] render helpers
        public static Rectangle GetItemRect() => new Rectangle(ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
        public static Vector2 GetNodeTitleBarOrigin(ImNodeData node) => node.Origin + node.LayoutStyle.Padding;

        //-------------------------------------------------------------------------
        public static Vector2 GetNodeContentOrigin(ImNodeData node)
        {
            Vector2 title_bar_height = new Vector2(0f, node.TitleBarContentRect.GetHeight() + 2.0f * node.LayoutStyle.Padding.Y);
            return node.Origin + title_bar_height + node.LayoutStyle.Padding;
        }

        //-------------------------------------------------------------------------
        public static Rectangle GetNodeTitleRect(ImNodeData node)
        {
            Rectangle expandedTitleRect = node.TitleBarContentRect;
            expandedTitleRect.Expand(node.LayoutStyle.Padding);

            return new Rectangle(
                expandedTitleRect.Min,
                expandedTitleRect.Min + new Vector2(node.Rect.GetWidth(), 0f) + new Vector2(0f, expandedTitleRect.GetHeight()));
        }

        //-------------------------------------------------------------------------
        public static void DrawGrid(ImNodesEditorContext editor, Vector2 canvasSize)
        {
            // Get the panning offset from the editor context.
            Vector2 offset = editor.Panning;

            // Retrieve the grid line colors from the style.
            uint lineColor = ImNodesCtx.Style.Colors[(int)ImNodesCol.GridLine];
            uint lineColorPrimary = ImNodesCtx.Style.Colors[(int)ImNodesCol.GridLinePrimary];
            bool drawPrimary = (ImNodesCtx.Style.Flags & ImNodesStyleFlags.GridLinesPrimary) != 0;

            float gridSpacing = ImNodesCtx.Style.GridSpacing;

            // Calculate starting x coordinate using modulus, adjusting negative remainders.
            float startX = offset.X % gridSpacing;
            if (startX < 0)
                startX += gridSpacing;

            // Draw vertical grid lines.
            for (float x = startX; x < canvasSize.X; x += gridSpacing)
            {
                Vector2 p1 = EditorSpaceToScreenSpace(new Vector2(x, 0f));
                Vector2 p2 = EditorSpaceToScreenSpace(new Vector2(x, canvasSize.Y));
                // If x exactly equals offset.X, use the primary color.
                uint col = ((offset.X - x) == 0f && drawPrimary) ? lineColorPrimary : lineColor;
                ImNodesCtx.CanvasDrawList.AddLine(p1, p2, col);
            }

            // Calculate starting y coordinate using modulus.
            float startY = offset.Y % gridSpacing;
            if (startY < 0)
                startY += gridSpacing;

            // Draw horizontal grid lines.
            for (float y = startY; y < canvasSize.Y; y += gridSpacing)
            {
                Vector2 p1 = EditorSpaceToScreenSpace(new Vector2(0f, y));
                Vector2 p2 = EditorSpaceToScreenSpace(new Vector2(canvasSize.X, y));
                uint col = ((offset.Y - y) == 0f && drawPrimary) ? lineColorPrimary : lineColor;
                ImNodesCtx.CanvasDrawList.AddLine(p1, p2, col);
            }
        }


        /// <summary>
        /// Updates the occludedPinIndices list with pins that are hidden (occluded) by nodes in front.
        /// </summary>
        /// <param name="editor">The current editor context.</param>
        /// <param name="occludedPinIndices">A list that will be populated with occluded pin indices.</param>
        //-------------------------------------------------------------------------
        public static void ResolveOccludedPins(ImNodesEditorContext editor, List<int> occludedPinIndices)
        {
            // Clear the list of occluded pins.
            occludedPinIndices.Clear();

            // If there are less than 2 nodes in depth, nothing can occlude.
            if (editor.NodeDepthOrder.Count < 2)
                return;

            // Iterate over each node in the depth order except the last one.
            for (int depthIdx = 0; depthIdx < editor.NodeDepthOrder.Count - 1; depthIdx++)
            {
                // Get the node that is potentially occluded.
                int nodeBelowIndex = editor.NodeDepthOrder[depthIdx];
                ImNodeData nodeBelow = editor.Nodes.Pool[nodeBelowIndex];

                // Iterate over the nodes on top of the current node.
                for (int nextDepthIdx = depthIdx + 1; nextDepthIdx < editor.NodeDepthOrder.Count; nextDepthIdx++)
                {
                    int nodeAboveIndex = editor.NodeDepthOrder[nextDepthIdx];
                    ImNodeData nodeAbove = editor.Nodes.Pool[nodeAboveIndex];
                    Rectangle rectAbove = nodeAbove.Rect;

                    // For each pin in the node below, check if its position is contained within the node above.
                    foreach (int pinIndex in nodeBelow.PinIndices)
                    {
                        Vector2 pinPos = editor.Pins.Pool[pinIndex].Pos;
                        if (rectAbove.Contains(pinPos))
                        {
                            occludedPinIndices.Add(pinIndex);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the index of the pin (from the pin pool) that is closest to the mouse and not occluded,
        /// or null if no pin is within the hover radius.
        /// </summary>
        /// <param name="pins">The pool of pins.</param>
        /// <param name="occludedPinIndices">List of pin indices occluded by nodes.</param>
        /// <returns>The hovered pin index or null.</returns>
        //-------------------------------------------------------------------------
        public static ImOptionalIndex ResolveHoveredPin(ImObjectPool<ImPinData> pins, List<int> occludedPinIndices)
        {
            float smallestDistance = float.MaxValue;
            ImOptionalIndex pinIndexWithSmallestDistance = new ImOptionalIndex();
            float hoverRadiusSqr = ImNodesCtx.Style.PinHoverRadius * ImNodesCtx.Style.PinHoverRadius;

            for (int idx = 0; idx < pins.Pool.Count; idx++)
            {
                if (!pins.InUse[idx])
                    continue;

                for (int i = 0; i < occludedPinIndices.Count; i++)
                {
                    if (occludedPinIndices[i] == idx)
                        continue;
                }

                Vector2 pinPos = pins.Pool[idx].Pos;
                float distanceSqr = Vector2.DistanceSquared(pinPos, ImNodesCtx.MousePos);

                if (distanceSqr < hoverRadiusSqr && distanceSqr < smallestDistance)
                {
                    smallestDistance = distanceSqr;
                    pinIndexWithSmallestDistance = idx;
                }
            }
            return pinIndexWithSmallestDistance;
        }

        /// <summary>
        /// Determines which node is hovered based on the overlapping nodes list and the depth stack.
        /// Returns the node index of the node on top or null if none.
        /// </summary>
        /// <param name="depthStack">A list of node indices in depth order (back-to-front).</param>
        /// <returns>The hovered node index or null.</returns>
        //-------------------------------------------------------------------------
        public static ImOptionalIndex ResolveHoveredNode(List<int> depthStack)
        {
            ImOptionalIndex result = new ImOptionalIndex();
            List<int> overlapping = ImNodesCtx.NodeIndicesOverlappingWithMouse;

            if (overlapping.Count == 0)
                return result;

            if (overlapping.Count == 1)
            {
                result = overlapping[0];
                return result;
            }

            int largestDepthIdx = -1;
            int nodeIdxOnTop = -1;
            for (int i = 0; i < overlapping.Count; i++)
            {
                for (int j = 0; j < depthStack.Count; j++)
                {
                    if (depthStack[j] == overlapping[i] && j > largestDepthIdx)
                    {
                        largestDepthIdx = j;
                        nodeIdxOnTop = overlapping[i];
                    }
                }
            }

            Debug.Assert(nodeIdxOnTop != -1);
            result = nodeIdxOnTop;
            return result;
        }

        /// <summary>
        /// Determines which link is hovered based on two criteria:
        /// 1. If a pin is hovered, only links attached to that pin are considered (and the first such link is returned).
        /// 2. Otherwise, the link whose cubic-bezier curve is closest to the mouse is returned.
        /// Returns the link index or null if no link qualifies.
        /// </summary>
        /// <param name="links">The pool of links.</param>
        /// <param name="pins">The pool of pins.</param>
        /// <returns>The hovered link index or null.</returns>
        //-------------------------------------------------------------------------
        public static ImOptionalIndex ResolveHoveredLink(ImObjectPool<ImLinkData> links, ImObjectPool<ImPinData> pins)
        {
            float smallestDistance = float.MaxValue;
            ImOptionalIndex linkIndexWithSmallestDistance = new ImOptionalIndex();

            for (int idx = 0; idx < links.Pool.Count; idx++)
            {
                if (!links.InUse[idx])
                    continue;

                ImLinkData link = links.Pool[idx];
                ImPinData startPin = pins.Pool[link.StartPinIdx];
                ImPinData endPin = pins.Pool[link.EndPinIdx];

                // If a pin is hovered, only consider links connected to that pin.
                if (ImNodesCtx.HoveredPinIdx.HasValue)
                {
                    if (ImNodesCtx.HoveredPinIdx.Value == link.StartPinIdx ||
                        ImNodesCtx.HoveredPinIdx.Value == link.EndPinIdx)
                    {
                        linkIndexWithSmallestDistance = idx;
                        return linkIndexWithSmallestDistance;
                    }
                    continue;
                }

                // Compute the cubic-bezier representation of the link.
                CubicBezier cubicBezier = CubicBezier.GetCubicBezier(startPin.Pos, endPin.Pos, startPin.Type, ImNodesCtx.Style.LinkLineSegmentsPerLength);

                // Get a bounding rectangle for the bezier curve.
                Rectangle linkRect = CubicBezier.GetContainingRectForCubicBezier(cubicBezier, ImNodesCtx.Style.LinkHoverDistance);

                // If the mouse is inside the bounding box, check the distance to the curve.
                if (linkRect.Contains(ImNodesCtx.MousePos))
                {
                    float distance = CubicBezier.GetDistanceToCubicBezier(ImNodesCtx.MousePos, cubicBezier, cubicBezier.NumSegments);
                    if (distance < ImNodesCtx.Style.LinkHoverDistance && distance < smallestDistance)
                    {
                        smallestDistance = distance;
                        linkIndexWithSmallestDistance = idx;
                    }
                }
            }

            return linkIndexWithSmallestDistance;
        }

        //-------------------------------------------------------------------------
        public static void BeginLinkInteraction(ImNodesEditorContext editor, int linkIdx, ImOptionalIndex pinIdx)
        {
            // Check if the modifier for link detach is pressed.
            bool modifierPressed = ImNodesCtx.Io.LinkDetachModifierClick.Modifier;
            if (modifierPressed)
            {
                var link = editor.Links.Pool[linkIdx];
                var startPin = editor.Pins.Pool[link.StartPinIdx];
                var endPin = editor.Pins.Pool[link.EndPinIdx];
                Vector2 mousePos = ImNodesCtx.MousePos;
                float distToStart = Vector2.DistanceSquared(startPin.Pos, mousePos);
                float distToEnd = Vector2.DistanceSquared(endPin.Pos, mousePos);
                int closestPinIdx = distToStart < distToEnd ? link.StartPinIdx : link.EndPinIdx;

                editor.ClickInteraction.Type = ImNodesClickInteractionType.LinkCreation;
                BeginLinkDetach(editor, linkIdx, closestPinIdx);
                editor.ClickInteraction.LinkCreation.Type = ImNodesLinkCreationType.FromDetach;
            }
            else
            {
                if (pinIdx.HasValue)
                {
                    ImNodesAttributeFlags hoveredPinFlags = editor.Pins.Pool[pinIdx.Value].Flags;
                    if ((hoveredPinFlags & ImNodesAttributeFlags.EnableLinkDetachWithDragClick) != 0)
                    {
                        BeginLinkDetach(editor, linkIdx, pinIdx.Value);
                        editor.ClickInteraction.LinkCreation.Type = ImNodesLinkCreationType.FromDetach;
                    }
                    else
                    {
                        BeginLinkCreation(editor, pinIdx.Value);
                    }
                }
                else
                {
                    BeginLinkSelection(editor, linkIdx);
                }
            }
        }

        //-------------------------------------------------------------------------
        public static void BeginLinkCreation(ImNodesEditorContext editor, int hoveredPinIdx)
        {
            editor.ClickInteraction.Type = ImNodesClickInteractionType.LinkCreation;
            editor.ClickInteraction.LinkCreation.StartPinIdx = hoveredPinIdx;
            editor.ClickInteraction.LinkCreation.EndPinIdx.Reset();
            editor.ClickInteraction.LinkCreation.Type = ImNodesLinkCreationType.Standard;
            ImNodesCtx.ImNodesUIState |= ImNodesUIState.LinkStarted;
        }

        //-------------------------------------------------------------------------
        public static void BeginLinkSelection(ImNodesEditorContext editor, int linkIdx)
        {
            // Set the click interaction type to indicate a link selection.
            editor.ClickInteraction.Type = ImNodesClickInteractionType.Link;

            // Clear any currently selected nodes or links.
            editor.SelectedNodeIndices.Clear();
            editor.SelectedLinkIndices.Clear();

            // Add the given link index as the sole selected link.
            editor.SelectedLinkIndices.Add(linkIdx);
        }

        //-------------------------------------------------------------------------
        public static void BeginLinkDetach(ImNodesEditorContext editor, int linkIdx, int detachPinIdx)
        {
            // Retrieve the link data from the editor's link pool.
            ImLinkData link = editor.Links.Pool[linkIdx];

            // Set the click interaction state to indicate a link creation event.
            editor.ClickInteraction.Type = ImNodesClickInteractionType.LinkCreation;

            // Reset the end pin index (using null to indicate that no end pin is set yet).
            editor.ClickInteraction.LinkCreation.EndPinIdx.Reset();

            // Set the start pin index for the new link.
            // If the detach_pin equals the link's start pin, then we choose the other pin as the starting point.
            editor.ClickInteraction.LinkCreation.StartPinIdx = (detachPinIdx == link.StartPinIdx)
                ? link.EndPinIdx
                : link.StartPinIdx;

            // Mark the link as deleted (indicating it was detached).
            ImNodesCtx.DeletedLinkIdx = linkIdx;
        }


        //-------------------------------------------------------------------------
        public static void BeginNodeSelection(ImNodesEditorContext editor, int node_idx)
        {
            // Don't start selecting if another click interaction (e.g. link creation) is in progress.
            if (editor.ClickInteraction.Type != ImNodesClickInteractionType.None)
                return;

            // Begin a node click interaction.
            editor.ClickInteraction.Type = ImNodesClickInteractionType.Node;

            // If the node is not already selected, select it.
            if (!editor.SelectedNodeIndices.Contains(node_idx))
            {
                // Clear any link selections.
                editor.SelectedLinkIndices.Clear();

                // If multiple selection is NOT active, clear the existing node selection.
                if (!ImNodesCtx.MultipleSelectModifier)
                {
                    editor.SelectedNodeIndices.Clear();
                }
                // Add this node to the selection.
                editor.SelectedNodeIndices.Add(node_idx);

                // Bring the selected node to the top of the depth order.
                List<int> depthStack = editor.NodeDepthOrder;
                int indexInDepth = depthStack.IndexOf(node_idx);
                //Debug.Assert(indexInDepth != -1, "Node not found in depth stack"); //FIXME
                if (indexInDepth != -1)
                    depthStack.RemoveAt(indexInDepth);
                depthStack.Add(node_idx);
            }
            // Otherwise, if the node is already selected and multiple selection is active, deselect it.
            else if (ImNodesCtx.MultipleSelectModifier)
            {
                int indexInSelection = editor.SelectedNodeIndices.IndexOf(node_idx);
                if (indexInSelection != -1)
                {
                    editor.SelectedNodeIndices.RemoveAt(indexInSelection);
                }
                // Don't allow dragging after deselecting.
                editor.ClickInteraction.Type = ImNodesClickInteractionType.None;
            }

            // For supporting snapping of multiple nodes, store each selected node's offset relative to the dragged node.
            Vector2 refOrigin = editor.Nodes.Pool[node_idx].Origin;
            editor.PrimaryNodeOffset = refOrigin + ImNodesCtx.CanvasOriginScreenSpace + editor.Panning - ImNodesCtx.MousePos;

            editor.SelectedNodeOffsets.Clear();
            foreach (int selectedNode in editor.SelectedNodeIndices)
            {
                Vector2 nodeOrigin = editor.Nodes.Pool[selectedNode].Origin - refOrigin;
                editor.SelectedNodeOffsets.Add(nodeOrigin);
            }
        }

        //-------------------------------------------------------------------------
        public static void BeginCanvasInteraction(ImNodesEditorContext editor)
        {
            bool anyUiElementHovered =
                ImNodesCtx.HoveredNodeIdx.HasValue ||
                ImNodesCtx.HoveredLinkIdx.HasValue ||
                ImNodesCtx.HoveredPinIdx.HasValue ||
                ImGui.IsAnyItemHovered();
            bool mouseNotInCanvas = !MouseInCanvas();

            if (editor.ClickInteraction.Type != ImNodesClickInteractionType.None ||
                anyUiElementHovered ||
                mouseNotInCanvas)
            {
                return;
            }

            bool startedPanning = ImNodesCtx.AltMouseClicked;
            if (startedPanning)
            {
                editor.ClickInteraction.Type = ImNodesClickInteractionType.Panning;
            }
            else if (ImNodesCtx.LeftMouseClicked)
            {
                editor.ClickInteraction.Type = ImNodesClickInteractionType.BoxSelection;
                editor.ClickInteraction.BoxSelector.Rect.Min = ScreenSpaceToGridSpace(editor, ImNodesCtx.MousePos);
            }
        }

        /// <summary>
        /// Sorts the draw list channels based on node depth.
        /// The method reorders the global NodeIdxSubmissionOrder list to match the provided depth order,
        /// swapping adjacent channels via DrawListSwapSubmissionIndices as needed.
        /// </summary>
        /// <param name="nodeIdxDepthOrder">List of node indices in desired (depth) order.</param>
        //-------------------------------------------------------------------------
        public static void DrawListSortChannelsByDepth(List<int> nodeIdxDepthOrder)
        {
            // Early out if there are fewer than 2 submission channels.
            if (ImNodesCtx.NodeIdxToSubmissionIdx.Data.Size < 2)
                return;

            Debug.Assert(nodeIdxDepthOrder.Count == ImNodesCtx.NodeIdxSubmissionOrder.Count);

            int startIdx = nodeIdxDepthOrder.Count - 1;

            // Find the first index (from the end) where the desired order does not match the current submission order.
            while (nodeIdxDepthOrder[startIdx] == ImNodesCtx.NodeIdxSubmissionOrder[startIdx])
            {
                if (--startIdx == 0)
                {
                    // Early exit if the orders are identical.
                    return;
                }
            }

            // For each node in the depth order from startIdx down to 1...
            for (int depthIdx = startIdx; depthIdx > 0; depthIdx--)
            {
                int nodeIdx = nodeIdxDepthOrder[depthIdx];

                // Find the current submission index for this node.
                int submissionIdx = -1;
                for (int i = 0; i < ImNodesCtx.NodeIdxSubmissionOrder.Count; i++)
                {
                    if (ImNodesCtx.NodeIdxSubmissionOrder[i] == nodeIdx)
                    {
                        submissionIdx = i;
                        break;
                    }
                }
                Debug.Assert(submissionIdx >= 0);

                if (submissionIdx == depthIdx)
                    continue;

                // Shift the node upward until it reaches the proper depth index.
                for (int j = submissionIdx; j < depthIdx; j++)
                {
                    DrawListSwapSubmissionIndices(j, j + 1);
                    // Swap adjacent elements in the submission order list.
                    int temp = ImNodesCtx.NodeIdxSubmissionOrder[j];
                    ImNodesCtx.NodeIdxSubmissionOrder[j] = ImNodesCtx.NodeIdxSubmissionOrder[j + 1];
                    ImNodesCtx.NodeIdxSubmissionOrder[j + 1] = temp;
                }
            }
        }

        //-------------------------------------------------------------------------
        public static void DrawListSwapSubmissionIndices(int lhs_idx, int rhs_idx)
        {
            Debug.Assert(lhs_idx != rhs_idx, "lhs_idx and rhs_idx must be different");

            int lhsForegroundChannelIdx = DrawListSubmissionIdxToForegroundChannelIdx(lhs_idx);
            int lhsBackgroundChannelIdx = DrawListSubmissionIdxToBackgroundChannelIdx(lhs_idx);
            int rhsForegroundChannelIdx = DrawListSubmissionIdxToForegroundChannelIdx(rhs_idx);
            int rhsBackgroundChannelIdx = DrawListSubmissionIdxToBackgroundChannelIdx(rhs_idx);

            ImDrawListSplitterSwapChannels(ImNodesCtx.CanvasDrawList, lhsBackgroundChannelIdx, rhsBackgroundChannelIdx);
            ImDrawListSplitterSwapChannels(ImNodesCtx.CanvasDrawList, lhsForegroundChannelIdx, rhsForegroundChannelIdx);
        }

        /// <summary>
        /// Swaps the command and index buffers between two channels in the splitter.
        /// Also updates the current channel index if needed.
        /// </summary>
        /// <param name="splitter">The ImDrawListSplitter instance.</param>
        /// <param name="lhsIdx">Index of the first channel.</param>
        /// <param name="rhsIdx">Index of the second channel.</param>
        //-------------------------------------------------------------------------
        public static void ImDrawListSplitterSwapChannels(ImDrawListPtr drawList, int lhsIdx, int rhsIdx)
        {
            if (lhsIdx == rhsIdx)
                return;

            Debug.Assert(lhsIdx >= 0 && lhsIdx < drawList.Splitter.Count, "lhsIdx is out of bounds");
            Debug.Assert(rhsIdx >= 0 && rhsIdx < drawList.Splitter.Count, "rhsIdx is out of bounds");

            // Retrieve channels from the splitter.
            ImDrawChannel lhsChannel = drawList.Splitter.Channels[lhsIdx];
            ImDrawChannel rhsChannel = drawList.Splitter.Channels[rhsIdx];

            // Swap the command buffers.
            ImVector<ImDrawCmd> tempCmdBuffer = lhsChannel.CmdBuffer;
            lhsChannel.CmdBuffer = rhsChannel.CmdBuffer;
            rhsChannel.CmdBuffer = tempCmdBuffer;

            // Swap the index buffers.
            ImVector<ushort> tempIdxBuffer = lhsChannel.IdxBuffer;
            lhsChannel.IdxBuffer = rhsChannel.IdxBuffer;
            rhsChannel.IdxBuffer = tempIdxBuffer;

            // If the current channel index equals one of the swapped indices, update it.
            if (drawList.Splitter.Current == lhsIdx)
            {
                drawList.Splitter.Current = rhsIdx;
            }
            else if (drawList.Splitter.Current == rhsIdx)
            {
                drawList.Splitter.Current = lhsIdx;
            }
        }

        //-------------------------------------------------------------------------
        public static bool IsMiniMapHovered()
        {
            // Assume EditorContextGet() returns the current editor context.
            ImNodesEditorContext editor = EditorContextGet();
            return IsMiniMapActive() &&
                   ImGui.IsMouseHoveringRect(editor.MiniMapRectScreenSpace.Min, editor.MiniMapRectScreenSpace.Max);
        }

        //-------------------------------------------------------------------------
        public static void DrawNode(ImNodesEditorContext editor, int nodeIdx)
        {
            var node = editor.Nodes.Pool[nodeIdx]; // <-- Nodes need to be added to pool, they are added to the nodes list above, which isn't used/drawn.

            // Position the ImGui cursor for node content.
            ImGui.SetCursorPos(node.Origin + editor.Panning);

            // Determine if the node is hovered (but not during box selection).
            bool nodeHovered = (ImNodesCtx.HoveredNodeIdx.HasValue && ImNodesCtx.HoveredNodeIdx.Value == nodeIdx) &&
                               (editor.ClickInteraction.Type != ImNodesClickInteractionType.BoxSelection);

            // Pick background and titlebar colors based on selection/hover state.
            uint nodeBackground = node.ColorStyle.Background;
            uint titlebarBackground = node.ColorStyle.Titlebar;
            if (editor.SelectedNodeIndices.Contains(nodeIdx))
            {
                nodeBackground = node.ColorStyle.BackgroundSelected;
                titlebarBackground = node.ColorStyle.TitlebarSelected;
            }
            else if (nodeHovered)
            {
                nodeBackground = node.ColorStyle.BackgroundHovered;
                titlebarBackground = node.ColorStyle.TitlebarHovered;
            }

            // Draw node background.
            ImNodesCtx.CanvasDrawList.AddRectFilled(node.Rect.Min, node.Rect.Max, nodeBackground, node.LayoutStyle.CornerRounding);

            // Draw the title bar if its content height is > 0.
            if (node.TitleBarContentRect.GetHeight() > 0f)
            {
                // Assume GetNodeTitleRect computes a rectangle for the title bar.
                Rectangle titleBarRect = GetNodeTitleRect(node);
                // Here we assume your drawing API exposes an overload that accepts a corner rounding flag for top corners.
                ImNodesCtx.CanvasDrawList.AddRectFilled(titleBarRect.Min, titleBarRect.Max, titlebarBackground, node.LayoutStyle.CornerRounding, ImDrawFlags.RoundCornersTop);
            }

            // Optionally draw an outline if the style requires it.
            if ((ImNodesCtx.Style.Flags & ImNodesStyleFlags.NodeOutline) != 0)
            {
                ImNodesCtx.CanvasDrawList.AddRect(node.Rect.Min, node.Rect.Max, node.ColorStyle.Outline, node.LayoutStyle.CornerRounding,
                                                 ImDrawFlags.RoundCornersAll, node.LayoutStyle.BorderThickness);
            }

            // Render all pins attached to the node.
            foreach (int pinIdx in node.PinIndices)
            {
                DrawPin(editor, pinIdx);
            }

            // If hovered, update the global hovered node index.
            if (nodeHovered)
            {
                ImNodesCtx.HoveredNodeIdx = nodeIdx;
            }
        }


        //-------------------------------------------------------------------------
        public static void DrawLink(ImNodesEditorContext editor, int linkIdx)
        {
            var link = editor.Links.Pool[linkIdx];
            var startPin = editor.Pins.Pool[link.StartPinIdx];
            var endPin = editor.Pins.Pool[link.EndPinIdx];

            // Compute the bezier curve for the link.
            CubicBezier cubicBezier = CubicBezier.GetCubicBezier(startPin.Pos, endPin.Pos, startPin.Type, ImNodesCtx.Style.LinkLineSegmentsPerLength);

            // Check if this link is hovered.
            bool linkHovered = ImNodesCtx.HoveredLinkIdx.HasValue && ImNodesCtx.HoveredLinkIdx.Value == linkIdx &&
                               (editor.ClickInteraction.Type != ImNodesClickInteractionType.BoxSelection);
            if (linkHovered)
            {
                ImNodesCtx.HoveredLinkIdx = linkIdx;
            }

            // If this link is marked as deleted, do not render.
            if (ImNodesCtx.DeletedLinkIdx.HasValue && ImNodesCtx.DeletedLinkIdx.Value == linkIdx)
            {
                return;
            }

            // Select link color based on selection and hover state.
            uint linkColor = link.ColorStyle.Base;
            if (editor.SelectedLinkIndices.Contains(linkIdx))
            {
                linkColor = link.ColorStyle.Selected;
            }
            else if (linkHovered)
            {
                linkColor = link.ColorStyle.Hovered;
            }

            // Draw the bezier curve. Use your draw list API which may vary depending on ImGui version.
            ImNodesCtx.CanvasDrawList.AddBezierCubic(
                cubicBezier.P0,
                cubicBezier.P1,
                cubicBezier.P2,
                cubicBezier.P3,
                linkColor,
                ImNodesCtx.Style.LinkThickness / editor.ZoomScale,
                cubicBezier.NumSegments);
        }

        //-------------------------------------------------------------------------
        public static void DrawPin(ImNodesEditorContext editor, int pinIdx)
        {
            // Retrieve the pin from the editor's pin pool.
            var pin = editor.Pins.Pool[pinIdx];

            // Get the parent node's rectangle.
            var parentNodeRect = editor.Nodes.Pool[pin.ParentNodeIdx].Rect;

            // Compute the pin's screen space position.
            pin.Pos = GetScreenSpacePinCoordinates(parentNodeRect, pin.AttributeRect, pin.Type);

            // Determine the pin's drawing color.
            uint pinColor = pin.ColorStyle.Background;
            if (ImNodesCtx.HoveredPinIdx.HasValue && ImNodesCtx.HoveredPinIdx.Value == pinIdx)
            {
                pinColor = pin.ColorStyle.Hovered;
            }

            // Draw the pin shape using a helper function.
            DrawPinShape(pin.Pos, pin, pinColor);
        }


        //-------------------------------------------------------------------------
        public static void DrawListAppendClickInteractionChannel()
        {
            // Assume that ImDrawListGrowChannels is a helper that adds the specified number of channels.
            ImDrawListGrowChannels(ImNodesCtx.CanvasDrawList, 1);
        }

        /// <summary>
        /// Increases the number of draw channels in the given draw list’s splitter by numChannels.
        /// </summary>
        /// <param name="drawList">The draw list whose channels are to be grown.</param>
        /// <param name="numChannels">The number of additional channels to add.</param>
        //-------------------------------------------------------------------------
        public static void ImDrawListGrowChannels(ImDrawListPtr drawList, int numChannels)
        {
            // If only one channel exists, call Split to create (numChannels + 1) channels.
            if (drawList.Splitter.Count == 1)
            {
                drawList.Splitter.Split(drawList, numChannels + 1);
                return;
            }

            int oldChannelCapacity = drawList.Splitter.Channels.Size;
            int oldChannelCount = drawList.Splitter.Count;
            int requestedChannelCount = oldChannelCount + numChannels;

            // If the current capacity is insufficient, expand the list.
            if (oldChannelCapacity < requestedChannelCount)
            {
                drawList.Splitter.Channels.Resize(requestedChannelCount);
                //int channelsToAdd = requestedChannelCount - oldChannelCapacity;
                //for (int i = 0; i < channelsToAdd; i++)
                //{
                //    // Add new channel instances.
                //    drawList.Splitter.Channels.PushBack(new ImDrawChannel());
                //}
            }

            // Update the active channel count.
            drawList.Splitter.Count = requestedChannelCount;

            // For each newly added channel, initialize its buffers and push an initial draw command.
            for (int i = oldChannelCount; i < requestedChannelCount; i++)
            {
                var channel = drawList.Splitter.Channels[i];
                if (i < oldChannelCapacity)
                {
                    // Reuse existing channel memory by clearing buffers.
                    channel.CmdBuffer.Clear();
                    channel.IdxBuffer.Clear();
                }
                else
                {
                    // Add new channel instances.
                    drawList.Splitter.Channels.PushBack(new ImDrawChannel());
                }
                //// Else, the new channel was just added via Add() above.

                // Create a new draw command.
                ImDrawCmd drawCmd = new ImDrawCmd();
                // Use the last clip rectangle and texture id from the draw list’s stacks.
                drawCmd.ClipRect = drawList.ClipRectStack[drawList.ClipRectStack.Size - 1];
                drawCmd.TextureId = drawList.TextureIdStack[drawList.TextureIdStack.Size - 1];

                // Add the draw command to the channel's command buffer.
                channel.CmdBuffer.PushBack(drawCmd);
            }
        }

        //-------------------------------------------------------------------------
        public static void DrawListActivateClickInteractionChannel()
        {
            // Set the current channel to the last one.
            int channelCount = ImNodesCtx.CanvasDrawList.Splitter.Count;
            ImNodesCtx.CanvasDrawList.Splitter.SetCurrentChannel(ImNodesCtx.CanvasDrawList, channelCount - 1);
        }

        //-------------------------------------------------------------------------
        public static Vector2 GetScreenSpacePinCoordinates(Rectangle node_rect, Rectangle attribute_rect, ImNodesAttributeType type)
        {
            Debug.Assert(type == ImNodesAttributeType.Input || type == ImNodesAttributeType.Output);
            float x = type == ImNodesAttributeType.Input
                                ? (node_rect.Min.X - ImNodesCtx.Style.PinOffset)
                                : (node_rect.Max.X + ImNodesCtx.Style.PinOffset);
            return new Vector2(x, 0.5f * (attribute_rect.Min.Y + attribute_rect.Max.Y));
        }

        //-------------------------------------------------------------------------
        public static Vector2 GetScreenSpacePinCoordinates(ImNodesEditorContext editor, ImPinData pin)
        {
            Rectangle parent_node_rect = editor.Nodes.Pool[pin.ParentNodeIdx].Rect;
            return GetScreenSpacePinCoordinates(parent_node_rect, pin.AttributeRect, pin.Type);
        }

        //-------------------------------------------------------------------------
        public static void CalcMiniMapLayout()
        {
            ImNodesEditorContext editor = EditorContextGet();
            // Offset and border are computed from style values divided by the current zoom scale.
            Vector2 offset = ImNodesCtx.Style.MiniMapOffset / editor.ZoomScale;
            Vector2 border = ImNodesCtx.Style.MiniMapPadding / editor.ZoomScale;
            // Use the canvas rectangle (in screen space).
            Rectangle editorRect = ImNodesCtx.CanvasRectScreenSpace;

            // Compute maximum mini-map size.
            Vector2 maxSize = ExtraMath.ImFloor((editorRect.GetSize() * editor.MiniMapSizeFraction) - (border * 2f));
            float maxAspect = maxSize.X / maxSize.Y;

            // Compute grid content size; if not set, use maxSize.
            Vector2 gridContentSize = editor.GridContentBounds.IsInverted() ? maxSize : ExtraMath.ImFloor(editor.GridContentBounds.GetSize());
            float gridAspect = gridContentSize.X / gridContentSize.Y;

            Vector2 miniMapSize;
            if (gridAspect > maxAspect)
            {
                miniMapSize = new Vector2(maxSize.X, maxSize.X / gridAspect);
            }
            else
            {
                miniMapSize = new Vector2(maxSize.Y * gridAspect, maxSize.Y);
            }
            float miniMapScaling = miniMapSize.X / gridContentSize.X;

            // Compute mini-map position.
            Vector2 align;
            switch (editor.MiniMapLocation)
            {
                case ImNodesMiniMapLocation.BottomRight:
                    align = new Vector2(1f, 1f);
                    break;
                case ImNodesMiniMapLocation.BottomLeft:
                    align = new Vector2(0f, 1f);
                    break;
                case ImNodesMiniMapLocation.TopRight:
                    align = new Vector2(1f, 0f);
                    break;
                case ImNodesMiniMapLocation.TopLeft:
                default:
                    align = new Vector2(0f, 0f);
                    break;
            }
            Vector2 topLeftPos = editorRect.Min + offset + border;
            Vector2 bottomRightPos = editorRect.Max - offset - border - miniMapSize;
            // Lerp between topLeftPos and bottomRightPos using align.
            Vector2 miniMapPos = ExtraMath.ImFloor(Vector2.Lerp(topLeftPos, bottomRightPos, align));

            // Set mini-map rectangles and scaling.
            editor.MiniMapRectScreenSpace = new Rectangle(miniMapPos - border, miniMapPos + miniMapSize + border);
            editor.MiniMapContentScreenSpace = new Rectangle(miniMapPos, miniMapPos + miniMapSize);
            editor.MiniMapScaling = miniMapScaling;
        }

        //-------------------------------------------------------------------------
        public static void MiniMapUpdate()
        {
            ImNodesEditorContext editor = EditorContextGet();
            uint miniMapBackground = 0;
            if (IsMiniMapHovered())
                miniMapBackground = ImNodesCtx.Style.Colors[(int)ImNodesCol.MiniMapBackgroundHovered];
            else
                miniMapBackground = ImNodesCtx.Style.Colors[(int)ImNodesCol.MiniMapBackground];

            // Create a child window for the mini-map.
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoBackground;
            ImGui.SetCursorScreenPos(editor.MiniMapRectScreenSpace.Min);
            ImGui.BeginChild("minimap", editor.MiniMapRectScreenSpace.GetSize(), ImGuiChildFlags.None, flags);

            Rectangle miniMapRect = editor.MiniMapRectScreenSpace;
            // Draw the mini-map background and outline.
            ImNodesCtx.CanvasDrawList.AddRectFilled(miniMapRect.Min, miniMapRect.Max, miniMapBackground);
            ImNodesCtx.CanvasDrawList.AddRect(miniMapRect.Min, miniMapRect.Max, ImNodesCtx.Style.Colors[(int)ImNodesCol.MiniMapOutline], 0, 0, 1 / editor.ZoomScale);

            // Push a clip rect to restrict drawing within the mini-map.
            ImNodesCtx.CanvasDrawList.PushClipRect(miniMapRect.Min, miniMapRect.Max, true);

            // Draw links first.
            for (int linkIdx = 0; linkIdx < editor.Links.Pool.Count; linkIdx++)
            {
                if (editor.Links.InUse[linkIdx])
                {
                    MiniMapDrawLink(editor, linkIdx);
                }
            }
            // Draw nodes.
            for (int nodeIdx = 0; nodeIdx < editor.Nodes.Pool.Count; nodeIdx++)
            {
                if (editor.Nodes.InUse[nodeIdx])
                {
                    MiniMapDrawNode(editor, nodeIdx);
                }
            }

            // Draw a rectangle representing the editor canvas within the mini-map.
            uint canvasColor = ImNodesCtx.Style.Colors[(int)ImNodesCol.MiniMapCanvas];
            uint outlineColor = ImNodesCtx.Style.Colors[(int)ImNodesCol.MiniMapCanvasOutline];
            Rectangle rect = ScreenSpaceToMiniMapSpace(editor, ImNodesCtx.CanvasRectScreenSpace);
            ImNodesCtx.CanvasDrawList.AddRectFilled(rect.Min, rect.Max, canvasColor);
            ImNodesCtx.CanvasDrawList.AddRect(rect.Min, rect.Max, outlineColor, 0, 0, 1 / editor.ZoomScale);

            ImNodesCtx.CanvasDrawList.PopClipRect();

            bool miniMapHovered = ImGui.IsWindowHovered();
            ImGui.EndChild();

            // If the mini-map is hovered and the left mouse button is down, re-center the canvas.
            if (miniMapHovered && ImGuiP.IsMouseDown(ImGuiMouseButton.Left) &&
                editor.ClickInteraction.Type == ImNodesClickInteractionType.None &&
                ImNodesCtx.NodeIdxSubmissionOrder.Count > 0)
            {
                Vector2 target = MiniMapSpaceToGridSpace(editor, ImGui.GetMousePos());
                Vector2 center = ImNodesCtx.CanvasRectScreenSpace.GetCenter();
                editor.Panning = ExtraMath.ImFloor(center - target);
            }

            // Reset any mini-map node hovering callback info.
            editor.MiniMapNodeHoveringCallback = null;
            editor.MiniMapNodeHoveringCallbackUserData = null;
        }

        //-------------------------------------------------------------------------
        public static void MiniMapDrawLink(ImNodesEditorContext editor, int linkIdx)
        {
            var link = editor.Links.Pool[linkIdx];
            var startPin = editor.Pins.Pool[link.StartPinIdx];
            var endPin = editor.Pins.Pool[link.EndPinIdx];

            // Transform the start and end pin positions into mini-map space.
            Vector2 startPos = ScreenSpaceToMiniMapSpace(editor, startPin.Pos);
            Vector2 endPos = ScreenSpaceToMiniMapSpace(editor, endPin.Pos);

            // Compute the cubic-bezier for the link using a scaled line-segments-per-length value.
            CubicBezier cubicBezier = CubicBezier.GetCubicBezier(
                startPos,
                endPos,
                startPin.Type,
                ImNodesCtx.Style.LinkLineSegmentsPerLength / editor.MiniMapScaling);

            // Do not render if this link was detached.
            if (ImNodesCtx.DeletedLinkIdx.HasValue && ImNodesCtx.DeletedLinkIdx.Value == linkIdx)
            {
                return;
            }

            // Choose link color: if the link is selected, use the "selected" color; otherwise, use the default mini-map link color.
            uint linkColor = editor.SelectedLinkIndices.Contains(linkIdx)
                                ? ImNodesCtx.Style.Colors[(int)ImNodesCol.MiniMapLinkSelected]
                                : ImNodesCtx.Style.Colors[(int)ImNodesCol.MiniMapLink];

            // Draw the cubic-bezier curve in the mini-map.
            ImNodesCtx.CanvasDrawList.AddBezierCubic(
                cubicBezier.P0,
                cubicBezier.P1,
                cubicBezier.P2,
                cubicBezier.P3,
                linkColor,
                ImNodesCtx.Style.LinkThickness * editor.MiniMapScaling / editor.ZoomScale,
                cubicBezier.NumSegments);
        }

        //-------------------------------------------------------------------------
        public static void MiniMapDrawNode(ImNodesEditorContext editor, int nodeIdx)
        {
            var node = editor.Nodes.Pool[nodeIdx];

            // Convert the node's rectangle from screen space to mini-map space.
            Rectangle nodeRect = ScreenSpaceToMiniMapSpace(editor, node.Rect);

            // Calculate mini-map node rounding by scaling the node's corner rounding.
            float miniMapNodeRounding = (float)System.Math.Floor(node.LayoutStyle.CornerRounding * editor.MiniMapScaling);

            uint miniMapNodeBackground;
            // If no interaction is active and the mouse is hovering this node's mini-map rectangle…
            if (editor.ClickInteraction.Type == ImNodesClickInteractionType.None && ImGui.IsMouseHoveringRect(nodeRect.Min, nodeRect.Max))
            {
                miniMapNodeBackground = ImNodesCtx.Style.Colors[(int)ImNodesCol.MiniMapNodeBackgroundHovered];

                // If a user callback is set for mini-map node hovering, call it.
                if (editor.MiniMapNodeHoveringCallback != null)
                {
                    editor.MiniMapNodeHoveringCallback(node.Id, editor.MiniMapNodeHoveringCallbackUserData != null ? editor.MiniMapNodeHoveringCallbackUserData : null);
                }
            }
            else if (editor.SelectedNodeIndices.Contains(nodeIdx))
            {
                miniMapNodeBackground = ImNodesCtx.Style.Colors[(int)ImNodesCol.MiniMapNodeBackgroundSelected];
            }
            else
            {
                miniMapNodeBackground = ImNodesCtx.Style.Colors[(int)ImNodesCol.MiniMapNodeBackground];
            }

            uint miniMapNodeOutline = ImNodesCtx.Style.Colors[(int)ImNodesCol.MiniMapNodeOutline];

            // Draw the mini-map node: a filled rectangle with rounded corners.
            ImNodesCtx.CanvasDrawList.AddRectFilled(nodeRect.Min, nodeRect.Max, miniMapNodeBackground, miniMapNodeRounding);
            // Draw the node outline on top.
            ImNodesCtx.CanvasDrawList.AddRect(nodeRect.Min, nodeRect.Max, miniMapNodeOutline, miniMapNodeRounding, 0, 1f / editor.ZoomScale);
        }

        //-------------------------------------------------------------------------
        public static void ClickInteractionUpdate(ImNodesEditorContext editor)
        {
            switch (editor.ClickInteraction.Type)
            {
                case ImNodesClickInteractionType.BoxSelection:
                    {
                        // Update box selector rectangle: set the maximum point from the current mouse position in grid space.
                        editor.ClickInteraction.BoxSelector.Rect.Max = ScreenSpaceToGridSpace(editor, ImNodesCtx.MousePos);

                        // Convert the rectangle from grid space to screen space.
                        var boxRect = editor.ClickInteraction.BoxSelector.Rect;
                        boxRect.Min = GridSpaceToScreenSpace(editor, boxRect.Min);
                        boxRect.Max = GridSpaceToScreenSpace(editor, boxRect.Max);

                        // Update selection based on the box rectangle.
                        BoxSelectorUpdateSelection(editor, boxRect);

                        // Retrieve colors from style.
                        uint boxSelectorColor = ImNodesCtx.Style.Colors[(int)ImNodesCol.BoxSelector];
                        uint boxSelectorOutline = ImNodesCtx.Style.Colors[(int)ImNodesCol.BoxSelectorOutline];

                        // Draw the selection rectangle.
                        ImNodesCtx.CanvasDrawList.AddRectFilled(boxRect.Min, boxRect.Max, boxSelectorColor);
                        ImNodesCtx.CanvasDrawList.AddRect(boxRect.Min, boxRect.Max, boxSelectorOutline);

                        // When the left mouse button is released, reorder the depth stack.
                        if (ImNodesCtx.LeftMouseReleased)
                        {
                            List<int> depthStack = editor.NodeDepthOrder;
                            List<int> selectedIndices = editor.SelectedNodeIndices;

                            // Only reorder if there are some selected nodes and fewer than the total number.
                            if (selectedIndices.Count > 0 && selectedIndices.Count < depthStack.Count)
                            {
                                int numMoved = 0;
                                // Iterate over the depth stack until all selected nodes are moved.
                                for (int i = 0; i < depthStack.Count - selectedIndices.Count; i++)
                                {
                                    // While the node at position i is selected, remove it and add it to the back.
                                    while (selectedIndices.Contains(depthStack[i]))
                                    {
                                        int nodeIdx = depthStack[i];
                                        depthStack.RemoveAt(i);
                                        depthStack.Add(nodeIdx);
                                        numMoved++;
                                    }
                                    if (numMoved == selectedIndices.Count)
                                        break;
                                }
                            }
                            // Reset click interaction type.
                            editor.ClickInteraction.Type = ImNodesClickInteractionType.None;
                        }
                    }
                    break;

                case ImNodesClickInteractionType.Node:
                    {
                        TranslateSelectedNodes(editor);
                        if (ImNodesCtx.LeftMouseReleased)
                            editor.ClickInteraction.Type = ImNodesClickInteractionType.None;
                    }
                    break;

                case ImNodesClickInteractionType.Link:
                    {
                        if (ImNodesCtx.LeftMouseReleased)
                            editor.ClickInteraction.Type = ImNodesClickInteractionType.None;
                    }
                    break;

                case ImNodesClickInteractionType.LinkCreation:
                    {
                        // Retrieve the start pin from the click interaction.
                        var startPin = editor.Pins.Pool[editor.ClickInteraction.LinkCreation.StartPinIdx.Value];

                        // Check for duplicate link if a hovered pin exists.
                        ImOptionalIndex maybeDuplicateLinkIdx = ImNodesCtx.HoveredPinIdx.HasValue
                            ? FindDuplicateLink(editor, editor.ClickInteraction.LinkCreation.StartPinIdx.Value, ImNodesCtx.HoveredPinIdx.Value)
                            : new ImOptionalIndex();

                        // Determine whether the link should snap.
                        bool shouldSnap = ImNodesCtx.HoveredPinIdx.HasValue &&
                                          ShouldLinkSnapToPin(editor, startPin, ImNodesCtx.HoveredPinIdx.Value, maybeDuplicateLinkIdx);

                        // Determine if the hovered pin changed compared to a stored end pin.
                        bool snappingPinChanged = editor.ClickInteraction.LinkCreation.EndPinIdx.HasValue &&
                                                    (ImNodesCtx.HoveredPinIdx != editor.ClickInteraction.LinkCreation.EndPinIdx);

                        // If the snapped pin changed and a snap link exists, detach the link.
                        if (snappingPinChanged && ImNodesCtx.SnapLinkIdx.HasValue)
                        {
                            BeginLinkDetach(editor, ImNodesCtx.SnapLinkIdx.Value, editor.ClickInteraction.LinkCreation.EndPinIdx.Value);
                        }

                        // Get the screen-space coordinates for the start pin.
                        Vector2 startPos = GetScreenSpacePinCoordinates(editor, startPin);
                        // Determine the end position: snap to hovered pin if applicable.
                        Vector2 endPos = shouldSnap
                            ? GetScreenSpacePinCoordinates(editor, editor.Pins.Pool[ImNodesCtx.HoveredPinIdx.Value])
                            : ImNodesCtx.MousePos;

                        // Compute the cubic-bezier curve for the link.
                        CubicBezier cubicBezier = CubicBezier.GetCubicBezier(startPos, endPos, startPin.Type, ImNodesCtx.Style.LinkLineSegmentsPerLength);

                        // Draw the link.
                        ImNodesCtx.CanvasDrawList.AddBezierCubic(
                            cubicBezier.P0,
                            cubicBezier.P1,
                            cubicBezier.P2,
                            cubicBezier.P3,
                            ImNodesCtx.Style.Colors[(int)ImNodesCol.Link],
                            ImNodesCtx.Style.LinkThickness / editor.ZoomScale,
                            cubicBezier.NumSegments);

                        // Determine if link creation should occur based on snap.
                        bool linkCreationOnSnap = ImNodesCtx.HoveredPinIdx.HasValue &&
                                                    ((editor.Pins.Pool[ImNodesCtx.HoveredPinIdx.Value].Flags & ImNodesAttributeFlags.EnableLinkCreationOnSnap) != 0);

                        // If not snapping, reset the end pin.
                        if (!shouldSnap)
                            editor.ClickInteraction.LinkCreation.EndPinIdx.Reset();

                        bool createLink = shouldSnap && (ImNodesCtx.LeftMouseReleased || linkCreationOnSnap);
                        if (createLink && !maybeDuplicateLinkIdx.HasValue)
                        {
                            // Avoid sending duplicate OnLinkCreated events if the hovered pin hasn't changed.
                            if (!ImNodesCtx.LeftMouseReleased &&
                                editor.ClickInteraction.LinkCreation.EndPinIdx == ImNodesCtx.HoveredPinIdx)
                            {
                                break; // Exit early.
                            }
                            ImNodesCtx.ImNodesUIState |= ImNodesUIState.LinkCreated;
                            editor.ClickInteraction.LinkCreation.EndPinIdx = ImNodesCtx.HoveredPinIdx.Value;
                        }

                        if (ImNodesCtx.LeftMouseReleased)
                        {
                            editor.ClickInteraction.Type = ImNodesClickInteractionType.None;
                            if (!createLink)
                                ImNodesCtx.ImNodesUIState |= ImNodesUIState.LinkDropped;
                        }
                    }
                    break;

                case ImNodesClickInteractionType.Panning:
                    {
                        bool dragging = ImNodesCtx.AltMouseDragging;
                        if (dragging)
                        {
                            editor.Panning += ImGui.GetIO().MouseDelta;
                        }
                        else
                        {
                            editor.ClickInteraction.Type = ImNodesClickInteractionType.None;
                        }
                    }
                    break;

                case ImNodesClickInteractionType.ImGuiItem:
                    {
                        if (ImNodesCtx.LeftMouseReleased)
                            editor.ClickInteraction.Type = ImNodesClickInteractionType.None;
                    }
                    break;

                case ImNodesClickInteractionType.None:
                    break;

                default:
                    Debug.Assert(false, "Unreachable code!");
                    break;
            }
        }

        //-------------------------------------------------------------------------
        public static bool ShouldLinkSnapToPin(ImNodesEditorContext editor, ImPinData startPin, int hoveredPinIdx, ImOptionalIndex duplicateLink)
        {
            // Retrieve the end pin from the pin pool.
            var end_pin = editor.Pins.Pool[hoveredPinIdx];

            // The end pin must be in a different node.
            if (startPin.ParentNodeIdx == end_pin.ParentNodeIdx)
                return false;

            // The end pin must be of a different type.
            if (startPin.Type == end_pin.Type)
                return false;

            // The link to be created must not be a duplicate,
            // unless it is the link which was created on snap.
            // In that case we want to snap.
            if (duplicateLink.HasValue && duplicateLink != ImNodesCtx.SnapLinkIdx)
                return false;

            return true;
        }


        /// <summary>
        /// Updates node and link selection based on the current box selector rectangle.
        /// This function assumes that 'boxRect' is given in screen space.
        /// </summary>
        //-------------------------------------------------------------------------
        public static void BoxSelectorUpdateSelection(ImNodesEditorContext editor, Rectangle boxRect)
        {
            // Ensure the rectangle's coordinates are in proper order.
            Vector2 newMin = new Vector2(MathF.Min(boxRect.Min.X, boxRect.Max.X), MathF.Min(boxRect.Min.Y, boxRect.Max.Y));
            Vector2 newMax = new Vector2(MathF.Max(boxRect.Min.X, boxRect.Max.X), MathF.Max(boxRect.Min.Y, boxRect.Max.Y));
            boxRect.Min = newMin;
            boxRect.Max = newMax;

            // Clear existing node selection.
            editor.SelectedNodeIndices.Clear();

            // For each node in the pool, if it's in use and its rectangle overlaps the box, select it.
            for (int nodeIdx = 0; nodeIdx < editor.Nodes.Pool.Count; nodeIdx++)
            {
                if (editor.Nodes.InUse[nodeIdx])
                {
                    ImNodeData node = editor.Nodes.Pool[nodeIdx];
                    if (boxRect.Overlaps(node.Rect))
                    {
                        editor.SelectedNodeIndices.Add(nodeIdx);
                    }
                }
            }

            // Clear existing link selection.
            editor.SelectedLinkIndices.Clear();

            // For each link, test for overlap.
            for (int linkIdx = 0; linkIdx < editor.Links.Pool.Count; linkIdx++)
            {
                if (editor.Links.InUse[linkIdx])
                {
                    ImLinkData link = editor.Links.Pool[linkIdx];
                    ImPinData pinStart = editor.Pins.Pool[link.StartPinIdx];
                    ImPinData pinEnd = editor.Pins.Pool[link.EndPinIdx];

                    // Retrieve the parent nodes' rectangles.
                    Rectangle nodeStartRect = editor.Nodes.Pool[pinStart.ParentNodeIdx].Rect;
                    Rectangle nodeEndRect = editor.Nodes.Pool[pinEnd.ParentNodeIdx].Rect;

                    // Compute screen-space positions for the pins.
                    Vector2 start = GetScreenSpacePinCoordinates(editor, nodeStartRect, pinStart.AttributeRect, pinStart.Type);
                    Vector2 end = GetScreenSpacePinCoordinates(editor, nodeEndRect, pinEnd.AttributeRect, pinEnd.Type);

                    // If the link overlaps the box, add it.
                    if (RectangleOverlapsLink(boxRect, start, end, pinStart.Type))
                    {
                        editor.SelectedLinkIndices.Add(linkIdx);
                    }
                }
            }
        }

        //-------------------------------------------------------------------------
        public static Vector2 GetScreenSpacePinCoordinates(ImNodesEditorContext editor, Rectangle nodeRect, Rectangle attributeRect, ImNodesAttributeType pinType)
        {
            // Typically, the pin’s X coordinate is derived from the node rectangle (and an offset)
            // and the Y coordinate is roughly the vertical center of the attribute rectangle.
            float x = (pinType == ImNodesAttributeType.Input) ? (nodeRect.Min.X - ImNodesCtx.Style.PinOffset)
                                                              : (nodeRect.Max.X + ImNodesCtx.Style.PinOffset);
            float y = (attributeRect.Min.Y + attributeRect.Max.Y) * 0.5f;
            return new Vector2(x, y);
        }

        //-------------------------------------------------------------------------
        public static bool RectangleOverlapsLink(Rectangle rect, Vector2 linkStart, Vector2 linkEnd, ImNodesAttributeType pinType)
        {
            // A simplified implementation:
            // First, create a bounding rectangle for the link.
            Vector2 min = new Vector2(MathF.Min(linkStart.X, linkEnd.X), MathF.Min(linkStart.Y, linkEnd.Y));
            Vector2 max = new Vector2(MathF.Max(linkStart.X, linkEnd.X), MathF.Max(linkStart.Y, linkEnd.Y));
            Rectangle linkRect = new Rectangle(min, max);
            // Check if the two rectangles overlap.
            return rect.Overlaps(linkRect);
        }

        //-------------------------------------------------------------------------
        public static ImOptionalIndex FindDuplicateLink(ImNodesEditorContext editor, int startPinIdx, int endPinIdx)
        {
            ImOptionalIndex duplicateLinkIdx = new ImOptionalIndex();

            // Create a test link with the given start and end pin indices.
            ImLinkData testLink = new ImLinkData(0);
            testLink.StartPinIdx = startPinIdx;
            testLink.EndPinIdx = endPinIdx;

            // Iterate over all links in the pool.
            for (int linkIdx = 0; linkIdx < editor.Links.Pool.Count; linkIdx++)
            {
                ImLinkData link = editor.Links.Pool[linkIdx];
                // Check for duplicate using our LinkPredicate and ensure the link slot is in use.
                if (LinkPredicate(testLink, link) && editor.Links.InUse[linkIdx])
                {
                    duplicateLinkIdx = linkIdx;
                    return duplicateLinkIdx;
                }
            }

            return duplicateLinkIdx;
        }

        /// <summary>
        /// Returns true if lhs and rhs are considered duplicates (i.e. they connect the same two pins regardless of order).
        /// </summary>
        //-------------------------------------------------------------------------
        public static bool LinkPredicate(ImLinkData lhs, ImLinkData rhs)
        {
            // Normalize the pin indices so that the smaller index is always first.
            int lhsStart = lhs.StartPinIdx, lhsEnd = lhs.EndPinIdx;
            if (lhsStart > lhsEnd)
            {
                int tmp = lhsStart;
                lhsStart = lhsEnd;
                lhsEnd = tmp;
            }

            int rhsStart = rhs.StartPinIdx, rhsEnd = rhs.EndPinIdx;
            if (rhsStart > rhsEnd)
            {
                int tmp = rhsStart;
                rhsStart = rhsEnd;
                rhsEnd = tmp;
            }

            return lhsStart == rhsStart && lhsEnd == rhsEnd;
        }

        //-------------------------------------------------------------------------
        private static float ImInvLength(Vector2 v, float defaultValue)
        {
            float length = v.Length();
            return length > 0.0f ? 1.0f / length : defaultValue;
        }

        //-------------------------------------------------------------------------
        public static void DrawPinShape(Vector2 pinPos, ImPinData pin, uint pinColor)
        {
            const int circleNumSegments = 8;
            switch (pin.Shape)
            {
                case ImNodesPinShape.Circle:
                    ImNodesCtx.CanvasDrawList.AddCircle(pinPos,
                                                      ImNodesCtx.Style.PinCircleRadius,
                                                      pinColor,
                                                      circleNumSegments,
                                                      ImNodesCtx.Style.PinLineThickness);
                    break;
                case ImNodesPinShape.CircleFilled:
                    ImNodesCtx.CanvasDrawList.AddCircleFilled(pinPos,
                                                            ImNodesCtx.Style.PinCircleRadius,
                                                            pinColor,
                                                            circleNumSegments);
                    break;
                case ImNodesPinShape.Quad:
                    {
                        QuadOffsets offset = CalculateQuadOffsets(ImNodesCtx.Style.PinQuadSideLength);
                        ImNodesCtx.CanvasDrawList.AddQuad(pinPos + offset.TopLeft,
                                                        pinPos + offset.BottomLeft,
                                                        pinPos + offset.BottomRight,
                                                        pinPos + offset.TopRight,
                                                        pinColor,
                                                        ImNodesCtx.Style.PinLineThickness);
                    }
                    break;
                case ImNodesPinShape.QuadFilled:
                    {
                        QuadOffsets offset = CalculateQuadOffsets(ImNodesCtx.Style.PinQuadSideLength);
                        ImNodesCtx.CanvasDrawList.AddQuadFilled(pinPos + offset.TopLeft,
                                                              pinPos + offset.BottomLeft,
                                                              pinPos + offset.BottomRight,
                                                              pinPos + offset.TopRight,
                                                              pinColor);
                    }
                    break;
                case ImNodesPinShape.Triangle:
                    {
                        TriangleOffsets offset = CalculateTriangleOffsets(ImNodesCtx.Style.PinTriangleSideLength);
                        // Multiply line thickness by 2 to mimic the C++ behavior.
                        ImNodesCtx.CanvasDrawList.AddTriangle(pinPos + offset.TopLeft,
                                                            pinPos + offset.BottomLeft,
                                                            pinPos + offset.Right,
                                                            pinColor,
                                                            2f * ImNodesCtx.Style.PinLineThickness);
                    }
                    break;
                case ImNodesPinShape.TriangleFilled:
                    {
                        TriangleOffsets offset = CalculateTriangleOffsets(ImNodesCtx.Style.PinTriangleSideLength);
                        ImNodesCtx.CanvasDrawList.AddTriangleFilled(pinPos + offset.TopLeft,
                                                                  pinPos + offset.BottomLeft,
                                                                  pinPos + offset.Right,
                                                                  pinColor);
                    }
                    break;
                default:
                    Debug.Assert(false, "Invalid PinShape value!");
                    break;
            }
        }

        //-------------------------------------------------------------------------
        public static void TranslateSelectedNodes(ImNodesEditorContext editor)
        {
            if (ImNodesCtx.LeftMouseDragging)
            {
                // If grid snapping is enabled, wait until the mouse has moved a sufficient distance.
                bool shouldTranslate = true;
                if ((ImNodesCtx.Style.Flags & ImNodesStyleFlags.GridSnapping) != 0)
                {
                    // Check if the drag distance squared is above a threshold.
                    shouldTranslate = ImGui.GetIO().MouseDragMaxDistanceSqr[0] > 5.0f;
                }

                // Compute the new origin by taking the current mouse position, subtracting the canvas origin
                // and panning, then adding the primary node offset.
                Vector2 origin = SnapOriginToGrid(
                    ImNodesCtx.MousePos - ImNodesCtx.CanvasOriginScreenSpace - editor.Panning + editor.PrimaryNodeOffset);

                // For each selected node, update its origin if it is draggable.
                for (int i = 0; i < editor.SelectedNodeIndices.Count; i++)
                {
                    Vector2 nodeRel = editor.SelectedNodeOffsets[i];
                    int nodeIdx = editor.SelectedNodeIndices[i];
                    var node = editor.Nodes.Pool[nodeIdx];
                    if (node.Draggable && shouldTranslate)
                    {
                        node.Origin = origin + nodeRel + editor.AutoPanningDelta;
                    }
                }
            }
        }

        //-------------------------------------------------------------------------
        public static Vector2 SnapOriginToGrid(Vector2 origin)
        {
            // Check if grid snapping is enabled.
            if ((ImNodesCtx.Style.Flags & ImNodesStyleFlags.GridSnapping) != 0)
            {
                float spacing = ImNodesCtx.Style.GridSpacing;
                float spacing2 = spacing * 0.5f;
                // Calculate the remainder of (absolute coordinate + half spacing) divided by spacing.
                float modx = ((MathF.Abs(origin.X) + spacing2) % spacing) - spacing2;
                float mody = ((MathF.Abs(origin.Y) + spacing2) % spacing) - spacing2;
                // Adjust origin.x based on its sign.
                origin.X += (origin.X < 0f) ? modx : -modx;
                // Adjust origin.y similarly.
                origin.Y += (origin.Y < 0f) ? mody : -mody;
            }
            return origin;
        }


        //-------------------------------------------------------------------------
        public static QuadOffsets CalculateQuadOffsets(float sideLength)
        {
            float halfSide = sideLength * 0.5f;
            return new QuadOffsets
            {
                TopLeft = new Vector2(-halfSide, halfSide),
                BottomLeft = new Vector2(-halfSide, -halfSide),
                BottomRight = new Vector2(halfSide, -halfSide),
                TopRight = new Vector2(halfSide, halfSide)
            };
        }

        //-------------------------------------------------------------------------
        public static TriangleOffsets CalculateTriangleOffsets(float sideLength)
        {
            float sqrt3 = (float)System.Math.Sqrt(3.0);
            float leftOffset = -0.1666666666667f * sqrt3 * sideLength;
            float rightOffset = 0.333333333333f * sqrt3 * sideLength;
            float verticalOffset = 0.5f * sideLength;

            return new TriangleOffsets
            {
                TopLeft = new Vector2(leftOffset, verticalOffset),
                BottomLeft = new Vector2(leftOffset, -verticalOffset),
                Right = new Vector2(rightOffset, 0f)
            };
        }


        //-------------------------------------------------------------------------
        // coordinate space conversion helpers
        //-------------------------------------------------------------------------

        //-------------------------------------------------------------------------
        public static void SetNodeGridSpacePos(int nodeId, Vector2 gridPos)
        {
            ImNodesEditorContext editor = EditorContextGet();
            ImNodeData node = editor.Nodes.ObjectPoolFindOrCreateObject(nodeId);
            node.Origin = gridPos;
        }

        //-------------------------------------------------------------------------
        public static Vector2 ScreenSpaceToGridSpace(ImNodesEditorContext editor, Vector2 v)
        {
            return v - ImNodesCtx.CanvasOriginScreenSpace - editor.Panning;
        }

        //-------------------------------------------------------------------------
        public static Rectangle ScreenSpaceToGridSpace(ImNodesEditorContext editor, Rectangle r)
        {
            return new Rectangle(ScreenSpaceToGridSpace(editor, r.Min), ScreenSpaceToGridSpace(editor, r.Max));
        }

        //-------------------------------------------------------------------------
        public static Vector2 GridSpaceToScreenSpace(ImNodesEditorContext editor, Vector2 v)
        {
            return v + ImNodesCtx.CanvasOriginScreenSpace + editor.Panning;
        }

        //-------------------------------------------------------------------------
        public static Vector2 GridSpaceToEditorSpace(ImNodesEditorContext editor, Vector2 v)
        {
            return v + editor.Panning;
        }

        //-------------------------------------------------------------------------
        public static Vector2 EditorSpaceToGridSpace(ImNodesEditorContext editor, Vector2 v)
        {
            return v - editor.Panning;
        }

        //-------------------------------------------------------------------------
        public static Vector2 EditorSpaceToScreenSpace(Vector2 v)
        {
            return ImNodesCtx.CanvasOriginScreenSpace + v;
        }

        //-------------------------------------------------------------------------
        public static Vector2 MiniMapSpaceToGridSpace(ImNodesEditorContext editor, Vector2 v)
        {
            return (v - editor.MiniMapContentScreenSpace.Min) / editor.MiniMapScaling + editor.GridContentBounds.Min;
        }

        //-------------------------------------------------------------------------
        public static Vector2 ScreenSpaceToMiniMapSpace(ImNodesEditorContext editor, Vector2 v)
        {
            return (ScreenSpaceToGridSpace(editor, v) - editor.GridContentBounds.Min) *
                       editor.MiniMapScaling +
                   editor.MiniMapContentScreenSpace.Min;
        }

        //-------------------------------------------------------------------------
        public static Rectangle ScreenSpaceToMiniMapSpace(ImNodesEditorContext editor, Rectangle r)
        {
            return new Rectangle(ScreenSpaceToMiniMapSpace(editor, r.Min), ScreenSpaceToMiniMapSpace(editor, r.Max));
        }

        //-------------------------------------------------------------------------
        public static void BeginStaticAttribute(int id)
        {
            // Ensure that BeginNode has already been called (current scope is Node)
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.Node, "BeginStaticAttribute must be called within a node.");
            // Switch to attribute scope.
            ImNodesCtx.CurrentScope = ImNodesScope.Attribute;

            // Set the current attribute ID.
            ImNodesCtx.CurrentAttributeId = id;

            // Begin an ImGui group and push a unique ID for this attribute.
            ImGui.BeginGroup();
            ImGui.PushID(id);
        }

        //-------------------------------------------------------------------------
        public static void EndStaticAttribute()
        {
            // Ensure we're in the attribute scope.
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.Attribute, "EndStaticAttribute must be called while in attribute scope.");
            // Return to Node scope.
            ImNodesCtx.CurrentScope = ImNodesScope.Node;

            // Pop the pushed ID and end the group.
            ImGui.PopID();
            ImGui.EndGroup();

            // If the attribute is active, mark it.
            if (ImGui.IsItemActive())
            {
                ImNodesCtx.ActiveAttribute = true;
                ImNodesCtx.ActiveAttributeId = ImNodesCtx.CurrentAttributeId;
            }
        }

        //-------------------------------------------------------------------------
        public static void Link(int id, int startAttributeId, int endAttributeId)
        {
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.Editor, "Link() must be called in Editor scope.");

            ImNodesEditorContext editor = EditorContextGet();
            // Retrieve (or create) the link object from the pool.
            var link = editor.Links.ObjectPoolFindOrCreateObject(id);
            link.Id = id;
            link.StartPinIdx = editor.Pins.ObjectPoolFindOrCreateIndex(startAttributeId);
            link.EndPinIdx = editor.Pins.ObjectPoolFindOrCreateIndex(endAttributeId);

            // Set link colors from the global style.
            link.ColorStyle.Base = ImNodesCtx.Style.Colors[(int)ImNodesCol.Link];
            link.ColorStyle.Hovered = ImNodesCtx.Style.Colors[(int)ImNodesCol.LinkHovered];
            link.ColorStyle.Selected = ImNodesCtx.Style.Colors[(int)ImNodesCol.LinkSelected];

            // Check if this link was created by the current link event.
            bool condition1 = editor.ClickInteraction.Type == ImNodesClickInteractionType.LinkCreation &&
                              ((editor.Pins.Pool[link.EndPinIdx].Flags & ImNodesAttributeFlags.EnableLinkCreationOnSnap) != 0) &&
                              (editor.ClickInteraction.LinkCreation.StartPinIdx.Value == link.StartPinIdx) &&
                              (editor.ClickInteraction.LinkCreation.EndPinIdx.HasValue &&
                               editor.ClickInteraction.LinkCreation.EndPinIdx.Value == link.EndPinIdx);

            bool condition2 = editor.ClickInteraction.LinkCreation.StartPinIdx.Value == link.EndPinIdx &&
                              editor.ClickInteraction.LinkCreation.EndPinIdx.HasValue &&
                              editor.ClickInteraction.LinkCreation.EndPinIdx.Value == link.StartPinIdx;

            if (condition1 || condition2)
            {
                ImNodesCtx.SnapLinkIdx = editor.Links.ObjectPoolFindOrCreateIndex(id);
            }
        }

        //-------------------------------------------------------------------------
        public static bool IsLinkCreated(out int startedAtPinId, out int endedAtPinId, out bool createdFromSnap)
        {
            // Check if the ImNodes UI state has the LinkCreated flag.
            bool isCreated = (ImNodesCtx.ImNodesUIState & ImNodesUIState.LinkCreated) != 0;
            if (isCreated)
            {
                ImNodesEditorContext editor = EditorContextGet();
                // Retrieve the start pin index from the click interaction.
                int startIdx = editor.ClickInteraction.LinkCreation.StartPinIdx.Value;
                // Retrieve the end pin index (assume it has a value)
                int endIdx = editor.ClickInteraction.LinkCreation.EndPinIdx.Value;

                var startPin = editor.Pins.Pool[startIdx];
                var endPin = editor.Pins.Pool[endIdx];

                // Decide ordering: if the start pin is of type Output, then it is the "start".
                if (startPin.Type == ImNodesAttributeType.Output)
                {
                    startedAtPinId = startPin.Id;
                    endedAtPinId = endPin.Id;
                }
                else
                {
                    startedAtPinId = endPin.Id;
                    endedAtPinId = startPin.Id;
                }

                createdFromSnap = (editor.ClickInteraction.Type == ImNodesClickInteractionType.LinkCreation);
                return true;
            }
            else
            {
                startedAtPinId = 0;
                endedAtPinId = 0;
                createdFromSnap = false;
                return false;
            }
        }

        //-------------------------------------------------------------------------
        public static bool IsLinkCreated(
                        ref int startedAtNodeId,
                        ref int startedAtPinId,
                        ref int endedAtNodeId,
                        ref int endedAtPinId,
                        ref bool createdFromSnap)
        {
            // Ensure we're in the proper scope.
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.None);

            bool isCreated = (ImNodesCtx.ImNodesUIState & ImNodesUIState.LinkCreated) != 0;
            if (isCreated)
            {
                ImNodesEditorContext editor = EditorContextGet();
                int startIdx = editor.ClickInteraction.LinkCreation.StartPinIdx.Value;
                int endIdx = editor.ClickInteraction.LinkCreation.EndPinIdx.Value;

                var startPin = editor.Pins.Pool[startIdx];
                var startNode = editor.Nodes.Pool[startPin.ParentNodeIdx];
                var endPin = editor.Pins.Pool[endIdx];
                var endNode = editor.Nodes.Pool[endPin.ParentNodeIdx];

                if (startPin.Type == ImNodesAttributeType.Output)
                {
                    startedAtPinId = startPin.Id;
                    startedAtNodeId = startNode.Id;
                    endedAtPinId = endPin.Id;
                    endedAtNodeId = endNode.Id;
                }
                else
                {
                    startedAtPinId = endPin.Id;
                    startedAtNodeId = endNode.Id;
                    endedAtPinId = startPin.Id;
                    endedAtNodeId = startNode.Id;
                }

                createdFromSnap = (editor.ClickInteraction.Type == ImNodesClickInteractionType.LinkCreation);
                return true;
            }
            else
            {
                startedAtNodeId = 0;
                startedAtPinId = 0;
                endedAtNodeId = 0;
                endedAtPinId = 0;
                createdFromSnap = false;
                return false;
            }
        }


        //-------------------------------------------------------------------------
        public static bool IsLinkDestroyed(ref int linkId)
        {
            // If DeletedLinkIdx is set, the link was detached.
            if (ImNodesCtx.DeletedLinkIdx.HasValue)
            {
                ImNodesEditorContext editor = EditorContextGet();
                int idx = ImNodesCtx.DeletedLinkIdx.Value;
                linkId = editor.Links.Pool[idx].Id;
                return true;
            }
            else
            {
                linkId = 0;
                return false;
            }
        }

        //-------------------------------------------------------------------------
        public static int NumSelectedLinks()
        {
            // Ensure we're not within a node or attribute scope.
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.None, "NumSelectedLinks must be called when not in a node/attribute scope.");
            ImNodesEditorContext editor = EditorContextGet();
            return editor.SelectedLinkIndices.Count;
        }

        //-------------------------------------------------------------------------
        public static int[] GetSelectedLinks()
        {
            ImNodesEditorContext editor = EditorContextGet();
            int count = editor.SelectedLinkIndices.Count;
            int[] linkIds = new int[count];
            for (int i = 0; i < count; i++)
            {
                int linkIdx = editor.SelectedLinkIndices[i];
                linkIds[i] = editor.Links.Pool[linkIdx].Id;
            }
            return linkIds;
        }

        //-------------------------------------------------------------------------
        public static int NumSelectedNodes()
        {
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.None, "NumSelectedNodes must be called when not in a node/attribute scope.");
            ImNodesEditorContext editor = EditorContextGet();
            return editor.SelectedNodeIndices.Count;
        }

        //-------------------------------------------------------------------------
        public static int[] GetSelectedNodes()
        {
            ImNodesEditorContext editor = EditorContextGet();
            int count = editor.SelectedNodeIndices.Count;
            int[] nodeIds = new int[count];
            for (int i = 0; i < count; i++)
            {
                int nodeIdx = editor.SelectedNodeIndices[i];
                nodeIds[i] = editor.Nodes.Pool[nodeIdx].Id;
            }
            return nodeIds;
        }

        /// <summary>
        /// Clears the current node selection.
        /// </summary>
        //-------------------------------------------------------------------------
        public static void ClearNodeSelection()
        {
            ImNodesEditorContext editor = EditorContextGet();
            editor.SelectedNodeIndices.Clear();
        }

        /// <summary>
        /// Clears selection for the node with the given ID.
        /// </summary>
        //-------------------------------------------------------------------------
        public static void ClearNodeSelection(int nodeId)
        {
            ImNodesEditorContext editor = EditorContextGet();
            editor.Nodes.ClearObjectSelection(editor.SelectedNodeIndices, nodeId);
        }

        /// <summary>
        /// Clears the current link selection.
        /// </summary>
        //-------------------------------------------------------------------------
        public static void ClearLinkSelection()
        {
            ImNodesEditorContext editor = EditorContextGet();
            editor.SelectedLinkIndices.Clear();
        }

        /// <summary>
        /// Clears selection for the link with the given ID.
        /// </summary>
        //-------------------------------------------------------------------------
        public static void ClearLinkSelection(int linkId)
        {
            ImNodesEditorContext editor = EditorContextGet();
            editor.Links.ClearObjectSelection(editor.SelectedLinkIndices, linkId);
        }

        /// <summary>
        /// Adds the node with the given ID to the current selection.
        /// </summary>
        //-------------------------------------------------------------------------
        public static void SelectNode(int nodeId)
        {
            ImNodesEditorContext editor = EditorContextGet();
            editor.Nodes.SelectObject(editor.SelectedNodeIndices, nodeId);
        }

        /// <summary>
        /// Adds the link with the given ID to the current selection.
        /// </summary>
        //-------------------------------------------------------------------------
        public static void SelectLink(int linkId)
        {
            ImNodesEditorContext editor = EditorContextGet();
            editor.Links.SelectObject(editor.SelectedLinkIndices, linkId);
        }

        /// <summary>
        /// Returns true if the node with the given ID is selected.
        /// </summary>
        //-------------------------------------------------------------------------
        public static bool IsNodeSelected(int nodeId)
        {
            ImNodesEditorContext editor = EditorContextGet();
            return editor.Nodes.IsObjectSelected(editor.SelectedNodeIndices, nodeId);
        }

        /// <summary>
        /// Returns true if the link with the given ID is selected.
        /// </summary>
        //-------------------------------------------------------------------------
        public static bool IsLinkSelected(int linkId)
        {
            ImNodesEditorContext editor = EditorContextGet();
            return editor.Links.IsObjectSelected(editor.SelectedLinkIndices, linkId);
        }

        //-------------------------------------------------------------------------
        public static bool IsLinkStarted(ref int startedAtPinId)
        {
            // Ensure we are not in an active node or attribute scope.
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.None, "IsLinkStarted must be called outside of active node/attribute scopes.");

            ImNodesEditorContext editor = EditorContextGet();
            bool isStarted = (ImNodesCtx.ImNodesUIState & ImNodesUIState.LinkStarted) != 0;
            if (isStarted)
            {
                int pinIdx = editor.ClickInteraction.LinkCreation.StartPinIdx.Value;
                startedAtPinId = editor.Pins.Pool[pinIdx].Id;
            }
            else
            {
                startedAtPinId = -1;
            }
            return isStarted;
        }

        //-------------------------------------------------------------------------
        public static bool IsEditorHovered() => MouseInCanvas();

        //-------------------------------------------------------------------------
        public static bool IsNodeHovered(ref int nodeId)
        {
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.None);
            Debug.Assert(nodeId != -1);

            bool isHovered = ImNodesCtx.HoveredNodeIdx.HasValue;
            if (isHovered)
            {
                ImNodesEditorContext editor = EditorContextGet();
                nodeId = editor.Nodes.Pool[ImNodesCtx.HoveredNodeIdx.Value].Id;
            }
            else
            {
                nodeId = -1;
            }
            return isHovered;
        }

        //-------------------------------------------------------------------------
        public static bool IsLinkHovered(ref int linkId)
        {
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.None);
            Debug.Assert(linkId != -1);

            bool isHovered = ImNodesCtx.HoveredLinkIdx.HasValue;
            if (isHovered)
            {
                ImNodesEditorContext editor = EditorContextGet();
                linkId = editor.Links.Pool[ImNodesCtx.HoveredLinkIdx.Value].Id;
            }
            else
            {
                linkId = -1;
            }
            return isHovered;
        }

        //-------------------------------------------------------------------------
        public static bool IsPinHovered(ref int attr)
        {
            Debug.Assert(ImNodesCtx.CurrentScope == ImNodesScope.None);
            Debug.Assert(attr != -1);

            bool isHovered = ImNodesCtx.HoveredPinIdx.HasValue;
            if (isHovered)
            {
                ImNodesEditorContext editor = EditorContextGet();
                attr = editor.Pins.Pool[ImNodesCtx.HoveredPinIdx.Value].Id;
            }
            else
            {
                attr = -1;
            }
            return isHovered;
        }


        //-------------------------------------------------------------------------
        public static void PushColorStyle(ImNodesCol item, uint color)
        {
            ImNodesCtx.ColorModifierStack.Add(new ImNodesColElement(ImNodesCtx.Style.Colors[(int)item], item));
            ImNodesCtx.Style.Colors[(int)item] = color;
        }

        //-------------------------------------------------------------------------
        public static void PopColorStyle()
        {
            Debug.Assert(ImNodesCtx.ColorModifierStack.Count > 0);
            ImNodesColElement elem = ImNodesCtx.ColorModifierStack.Last();
            ImNodesCtx.Style.Colors[(int)elem.Item] = elem.Color;
            ImNodesCtx.ColorModifierStack.RemoveAt(ImNodesCtx.ColorModifierStack.Count - 1);
        }

        ////-------------------------------------------------------------------------
        //// New Node Drawing
        ////-------------------------------------------------------------------------
        //private static readonly List<Node> _nodes = new();
        //private static readonly List<Link> _links = new();

        //public static event EventHandler<Node> NodeAdded;
        //public static event EventHandler<Node> NodeRemoved;
        //public static event EventHandler<Link> LinkAdded;
        //public static event EventHandler<Link> LinkRemoved;

        ////-------------------------------------------------------------------------
        //public static virtual void Initialize()
        //{
        //    if (ImNodesCtx == null)
        //    {
        //        CreateContext();
        //    }

        //    //if (ImNodesCtx.EditorCtx != null)
        //    {
        //        for (int i = 0; i < _nodes.Count; i++)
        //        {
        //            _nodes[i].Initialize(this);
        //        }
        //        for (int i = 0; i < _links.Count; i++)
        //        {
        //            _links[i].Initialize(this);
        //        }
        //    }
        //}

        ////-------------------------------------------------------------------------
        //public static Node GetNode(int id)
        //{
        //    for (int i = 0; i < _nodes.Count; i++)
        //    {
        //        Node node = _nodes[i];
        //        if (node.Id == id)
        //            return node;
        //    }
        //    throw new();
        //}

        ////-------------------------------------------------------------------------
        //public static T GetNode<T>() where T : Node
        //{
        //    for (int i = 0; i < _nodes.Count; i++)
        //    {
        //        var node = _nodes[i];
        //        if (node is T t)
        //            return t;
        //    }
        //    throw new KeyNotFoundException();
        //}

        ////-------------------------------------------------------------------------
        //public static Link GetLink(int id)
        //{
        //    for (int i = 0; i < _links.Count; i++)
        //    {
        //        var link = _links[i];
        //        if (link.Id == id)
        //            return link;
        //    }

        //    throw new KeyNotFoundException();
        //}

        ////-------------------------------------------------------------------------
        //public static Node CreateNode(string name, bool removable = true, bool isStatic = false)
        //{
        //    Node node = new(GetUniqueId(), name, removable, isStatic);
        //    AddNode(node);
        //    return node;
        //}

        ////-------------------------------------------------------------------------
        //public static void AddNode(Node node)
        //{
        //    ImNodesEditorContext editor = EditorContextGet();
        //    if (editor == null)
        //        node.Initialize(this);

        //    _nodes.Add(node);
        //    NodeAdded?.Invoke(this, node);
        //}

        ////-------------------------------------------------------------------------
        //public static void RemoveNode(Node node)
        //{
        //    _nodes.Remove(node);
        //    NodeRemoved?.Invoke(this, node);
        //}

        ////-------------------------------------------------------------------------
        //public static void AddLink(Link link)
        //{
        //    ImNodesEditorContext editor = EditorContextGet();
        //    if (editor == null)
        //        link.Initialize(this);
        //    _links.Add(link);
        //    LinkAdded?.Invoke(this, link);
        //}

        ////-------------------------------------------------------------------------
        //public static void RemoveLink(Link link)
        //{
        //    _links.Remove(link);
        //    LinkRemoved?.Invoke(this, link);
        //}

        ////-------------------------------------------------------------------------
        //public static Link CreateLink(Pin input, Pin output)
        //{
        //    Link link = new Link(GetUniqueId(), output.Parent, output, input.Parent, input);
        //    AddLink(link);
        //    return link;
        //}

        ////-------------------------------------------------------------------------
        //public static void Draw()
        //{
        //    ImNodesEditorContext editor = EditorContextGet();
        //    //if (editor == null)
        //    //    Initialize();

        //    EditorContextSet(editor);
        //    BeginNodeEditor();
        //    for (int i = 0; i < _nodes.Count; i++)
        //    {
        //        _nodes[i].Draw();
        //    }
        //    for (int i = 0; i < _links.Count; i++)
        //    {
        //        _links[i].Draw();
        //    }

        //    EndNodeEditor();

        //    int idNode1 = 0;
        //    int idNode2 = 0;
        //    int idpin1 = 0;
        //    int idpin2 = 0;
        //    bool createdFromSnap = false;
        //    if (IsLinkCreated(ref idNode1, ref idpin1, ref idNode2, ref idpin2, ref createdFromSnap))
        //    {
        //        var pino = GetNode(idNode1).GetOuput(idpin1);
        //        var pini = GetNode(idNode2).GetInput(idpin2);
        //        if (pini.CanCreateLink(pino) && pino.CanCreateLink(pini))
        //            CreateLink(pini, pino);
        //    }
        //    int idLink = 0;
        //    if (IsLinkDestroyed(ref idLink))
        //    {
        //        GetLink(idLink).Destroy();
        //    }
        //    if (ImGuiP.IsKeyPressed(ImGuiKey.Delete))
        //    {
        //        int numLinks = NumSelectedLinks();
        //        if (numLinks != 0)
        //        {
        //            int[] links = GetSelectedLinks();
        //            for (int i = 0; i < links.Length; i++)
        //            {
        //                GetLink(links[i]).Destroy();
        //            }
        //        }
        //        int numNodes = NumSelectedNodes();
        //        if (numNodes != 0)
        //        {
        //            int[] nodes = GetSelectedNodes();
        //            for (int i = 0; i < nodes.Length; i++)
        //            {
        //                var node = GetNode(nodes[i]);
        //                if (node.Removable)
        //                {
        //                    node.Destroy();
        //                }
        //            }
        //        }
        //    }
        //    int idpinStart = 0;
        //    if (IsLinkStarted(ref idpinStart))
        //    {
        //    }

        //    for (int i = 0; i < _nodes.Count; i++)
        //    {
        //        var id = _nodes[i].Id;
        //        _nodes[i].IsHovered = IsNodeHovered(ref id);
        //    }

        //    if (IsEditorHovered() && ImGui.GetIO().MouseWheel != 0)
        //    {
        //        float zoom = EditorContextGetZoom() + ImGui.GetIO().MouseWheel * 0.1f;
        //        EditorContextSetZoom(zoom, ImGui.GetMousePos());
        //    }

        //    //EditorContextSet(null);

        //    //if (state != null)
        //    //{
        //    //    RestoreState(state);
        //    //    state = null;
        //    //}
        //}

        ////-------------------------------------------------------------------------
        //public static void Destroy()
        //{
        //    Node[] nodes = _nodes.ToArray();
        //    for (int i = 0; i < nodes.Length; i++)
        //    {
        //        nodes[i].Destroy();
        //    }
        //    _nodes.Clear();
        //}

        ////-------------------------------------------------------------------------
        //public static bool Validate(Pin startPin, Pin endPin)
        //{
        //    Node node = startPin.Parent;
        //    Stack<(int, Node)> walkstack = new();
        //    walkstack.Push((0, node));
        //    while (walkstack.Count > 0)
        //    {
        //        (int i, node) = walkstack.Pop();
        //        if (i > node.Links.Count)
        //            continue;
        //        Link link = node.Links[i];
        //        i++;
        //        walkstack.Push((i, node));
        //        if (link.OutputNode == node)
        //        {
        //            if (link.Output == endPin)
        //                return true;
        //            else
        //                walkstack.Push((0, link.InputNode));
        //        }
        //    }

        //    return false;
        //}
    }
}