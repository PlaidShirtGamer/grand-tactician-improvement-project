using System;
using UnityEngine;

namespace GTIP
{
    // Cursor lock is process-wide Unity state, not a replicated campaign field.
    // Retain guest ownership through disconnect and menu return until the next
    // local game/host starts, so skipped native UI callbacks cannot latch it.
    public sealed class MpCursorGuard
    {
        public bool Owned {get;private set;}
        public bool ReturnedToMenu {get;private set;}
        public Action<string> Diagnostic {get;set;}
        string lastBlockedSource;
        CursorLockMode lastBlockedMode;
        bool lastBlockedFocus;
        public void BeginGuest()
        {
            Owned=true;ReturnedToMenu=false;lastBlockedSource=null;
            Diagnostic?.Invoke("Guest cursor recovery enabled; focused="+Application.isFocused+", lock="+Cursor.lockState+", visible="+Cursor.visible);
            Repair("guest join");
        }
        public void EndSession(){Repair("session end");}
        public void MainMenu(){if(!Owned)return;ReturnedToMenu=true;Repair("main menu");}
        public void ReleaseOwnership(){Repair("local gameplay");Owned=false;ReturnedToMenu=false;lastBlockedSource=null;}
        public void Focus(bool focused)
        {
            if(!Owned)return;
            Diagnostic?.Invoke("Guest window focus="+focused+", cursor lock="+Cursor.lockState+", visible="+Cursor.visible);
            lastBlockedSource=null;Repair("window focus");
        }
        public void Repair(string reason)
        {
            if(!Owned)return;
            var old=Cursor.lockState;bool visible=Cursor.visible;
            if(old==CursorLockMode.None && visible)return;
            if(old!=CursorLockMode.None)Cursor.lockState=CursorLockMode.None;
            if(!visible)Cursor.visible=true;
            Diagnostic?.Invoke("Guest cursor recovered: reason="+reason+", previousLock="+old+", previousVisible="+visible+", focused="+Application.isFocused);
        }
        public void SetLock(CursorLockMode requested,string source)
        {
            if(!Owned){Cursor.lockState=requested;return;}
            if(requested!=CursorLockMode.None && (lastBlockedSource!=source || lastBlockedMode!=requested || lastBlockedFocus!=Application.isFocused)) {
                Diagnostic?.Invoke("Guest cursor lock prevented: source="+source+", requested="+requested+", focused="+Application.isFocused);
                lastBlockedSource=source;lastBlockedMode=requested;lastBlockedFocus=Application.isFocused;
            }
            Repair("native "+source);
        }
    }
}
