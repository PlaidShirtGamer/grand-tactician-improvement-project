using System;

namespace GTIP
{
    // Only a redraw is recoverable here. Restoration and subsequent native-state
    // verification stay outside the catch: a real state failure still ends replication.
    public static class MpPresentationFence
    {
        public static bool Refresh(Action draw,Action restore,Action<Exception> report)
        {
            Exception failure=null;
            try {draw();}
            catch(Exception error){failure=error;}
            finally {restore();}
            if(failure==null)return true;
            report(failure);return false;
        }
    }
}
