namespace ImGuiFramework.Input.Events
{
    public class KeyboardCharEventArgs : EventArgs
    {
        public KeyboardCharEventArgs()
        {
        }

        public char Char { get; internal set; }
    }
}