using System.Collections.Generic;

namespace GTIP
{
    // Shared ownership across the native Panel and GameObject pause registries.
    public sealed class MpPanelPause
    {
        readonly HashSet<object> open=new HashSet<object>();
        bool previouslyPaused;
        public bool Update(object panel,bool visible,bool paused)
        {
            if(visible && open.Count==0 && !open.Contains(panel))previouslyPaused=paused;
            if(visible)open.Add(panel);else open.Remove(panel);
            return open.Count>0 || previouslyPaused;
        }
        public void ManualPause(bool paused){if(open.Count>0)previouslyPaused=paused;}
        public void Clear(){open.Clear();previouslyPaused=false;}
    }
}
