namespace ImNodesCSharp.Demo
{
    using Hexa.NET.ImGui;
    using System.Numerics;
    using System.Xml.Linq;

    public class ImNodesDemo
    {
        public ImNodesDemo()
        {
        }

        //public void Initialize() => ImNodes.SetNodeGridSpacePos(1, new Vector2(200.0f, 200.0f));

        public void Shutdown() => ImNodes.DestroyContext();

        public void Draw()
        {
            ImGui.Begin("simple node Editor");

            ImNodesEditorContext editor = ImNodes.EditorContextGet();
            ImNodes.EditorContextSet(editor);
            ImNodes.BeginNodeEditor();

            ImNodes.BeginNode(0);

            ImNodes.BeginNodeTitleBar();
            ImGui.TextUnformatted("simple node :)");
            ImNodes.EndNodeTitleBar();

            ImNodes.BeginInputAttribute(2);
            ImGui.Text("input");
            ImNodes.EndInputAttribute();

            ImNodes.BeginOutputAttribute(3);
            ImGui.Indent(40);
            ImGui.Text("output");
            ImNodes.EndOutputAttribute();

            ImNodes.EndNode();
            ImNodes.EndNodeEditor();

            if (ImNodes.IsEditorHovered() && ImGui.GetIO().MouseWheel != 0)
            {
                ImNodesEditorContext editorCtx = ImNodes.EditorContextGet();
                float zoom = editorCtx.ZoomScale + ImGui.GetIO().MouseWheel * 0.1f;
                ImNodes.EditorContextSetZoom(zoom, ImGui.GetMousePos());
            }

            ImGui.End();
        }
    }
}
