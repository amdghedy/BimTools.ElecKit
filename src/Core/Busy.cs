using System.Windows.Input;

namespace ElecKit.Core
{
    /// <summary>Shows the wait cursor during long work. ElecCommand always switches it off again, even after an error.</summary>
    public static class Busy
    {
        public static void On() { Mouse.OverrideCursor = Cursors.Wait; }

        public static void Off() { Mouse.OverrideCursor = null; }
    }
}
