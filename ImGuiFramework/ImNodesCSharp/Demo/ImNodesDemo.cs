namespace ImNodesCSharp.Demo
{
    using Hexa.NET.ImGui;
    using System.Numerics;
    using System.Xml.Linq;

    public class ImNodesDemo
    {
        public ImNodes ImNodes { get; private set; } = new ImNodes();

        public ImNodesDemo()
        {
        }

        public void Draw()
        {
            ImGui.Begin("simple node Editor");

            ImNodes.BeginNodeEditor();

            ImNodes.BeginNode(1);

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

            ImGui.End();
        }
    }
}
