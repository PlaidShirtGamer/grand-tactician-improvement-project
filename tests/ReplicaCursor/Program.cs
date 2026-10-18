using GTIP;
using UnityEngine;

int checks = 0;
void Check(bool valid, string description)
{
    if (!valid) throw new Exception(description);
    checks++;
}

var diagnostics = new List<string>();
var guard = new MpCursorGuard { Diagnostic = diagnostics.Add };
Cursor.lockState = CursorLockMode.None;
Cursor.visible = true;
Application.isFocused = true;

Check(!guard.Owned, "Cursor ownership starts outside the guest session.");
guard.SetLock(CursorLockMode.Locked, "offline native focus");
Check(Cursor.lockState == CursorLockMode.Locked, "Offline native cursor locking passes through.");
guard.SetLock(CursorLockMode.Confined, "offline confined cursor");
Check(Cursor.lockState == CursorLockMode.Confined, "Offline confinement passes through unchanged.");
guard.SetLock(CursorLockMode.None, "offline focused cursor");
Check(Cursor.lockState == CursorLockMode.None, "Offline native unlocking passes through.");

Cursor.lockState = CursorLockMode.Locked;
Cursor.visible = false;
guard.BeginGuest();
Check(guard.Owned, "Joining claims guest cursor ownership.");
Check(!guard.ReturnedToMenu, "Joining clears the previous menu boundary.");
Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "Joining repairs a cursor inherited locked and hidden.");
Check(diagnostics.Count > 0, "Recovering a locked cursor emits a diagnostic.");

int stableDiagnostics = diagnostics.Count;
guard.Repair("steady focused frame");
guard.Repair("steady focused frame");
Check(diagnostics.Count == stableDiagnostics, "Healthy guest frames do not flood diagnostics.");
guard.SetLock(CursorLockMode.Locked, "BattleUI unfocused");
Check(Cursor.lockState == CursorLockMode.None, "A native background-focus lock cannot trap a guest cursor.");
guard.SetLock(CursorLockMode.Confined, "another native component");
Check(Cursor.lockState == CursorLockMode.None, "A native confinement request does not capture guest input.");
guard.SetLock(CursorLockMode.Locked, "MouseOrbitImproved Escape");
Check(Cursor.lockState == CursorLockMode.None, "Escape in an orbit component cannot center-lock the guest cursor.");

Application.isFocused = false;
Cursor.lockState = CursorLockMode.Locked;
Cursor.visible = false;
guard.Focus(false);
Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "Losing focus relinquishes guest cursor capture.");
Application.isFocused = true;
Cursor.lockState = CursorLockMode.Locked;
Cursor.visible = false;
guard.Focus(true);
Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "Regaining focus repairs an externally relocked cursor.");
Cursor.visible = false;
guard.Repair("hidden cursor only");
Check(Cursor.visible, "Recovery also repairs a hidden cursor without requiring a lock change.");

Cursor.lockState = CursorLockMode.Locked;
Cursor.visible = false;
guard.EndSession();
Check(guard.Owned && !guard.ReturnedToMenu, "Ending a session retains ownership during the return transition.");
Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "Disconnect repairs the cursor before returning to the menu.");
Cursor.lockState = CursorLockMode.Locked;
guard.MainMenu();
Check(guard.Owned && guard.ReturnedToMenu, "Returning to the menu preserves recovery ownership.");
Check(Cursor.lockState == CursorLockMode.None, "The menu repairs a surviving campaign lock.");
guard.EndSession();
Check(guard.ReturnedToMenu, "Repeated disconnect cleanup does not erase the observed menu boundary.");
guard.SetLock(CursorLockMode.Locked, "main menu native focus");
Check(Cursor.lockState == CursorLockMode.None, "Native cursor requests remain safe in the guest's main menu.");
guard.BeginGuest();
Check(!guard.ReturnedToMenu, "Rejoining starts a new return-to-menu boundary.");

guard.EndSession();
guard.MainMenu();
Cursor.lockState = CursorLockMode.Locked;
Cursor.visible = false;
guard.ReleaseOwnership();
Check(!guard.Owned, "A later offline campaign can release guest cursor ownership.");
Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "Releasing ownership first restores usable cursor state.");
guard.SetLock(CursorLockMode.Locked, "new offline campaign focus");
Check(Cursor.lockState == CursorLockMode.Locked, "Native cursor behavior resumes after leaving guest ownership.");
Cursor.visible = false;
guard.Repair("unowned frame");
Check(Cursor.lockState == CursorLockMode.Locked && !Cursor.visible, "Unowned recovery never changes offline cursor behavior.");

Console.WriteLine($"Replica cursor: {checks} checks passed using the production guard with Unity state doubles; no live game UI was exercised.");

namespace UnityEngine
{
    public enum CursorLockMode { None, Locked, Confined }
    public static class Cursor
    {
        public static CursorLockMode lockState;
        public static bool visible;
    }
    public static class Application
    {
        public static bool isFocused;
    }
}
