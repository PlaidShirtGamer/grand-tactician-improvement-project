using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        BaseInputModule[] blockedModules = new BaseInputModule[0];
        Texture2D white;
        void BlockInput()
        {
            if (blockedModules.Length != 0) return;
            var events = EventSystem.current;
            if(events!=null) events.SetSelectedGameObject(null);
            blockedModules = events == null ? new BaseInputModule[0] : events.GetComponents<BaseInputModule>().Where(m => m.enabled).ToArray();
            foreach (var module in blockedModules) module.enabled = false;
        }
        void RestoreInput() { if (IsReplica) return; foreach (var module in blockedModules) if (module != null) module.enabled = true; blockedModules = new BaseInputModule[0]; }
        void ClosePanel()
        {
            panel=false;
            suppressInputUntilFrame=Time.frameCount+1;
            GUIUtility.keyboardControl=0;
            var events=EventSystem.current;
            if(events!=null) events.SetSelectedGameObject(null);
            GameVars.deactivateshortcuts=false;
            RestoreInput();
        }
        void Try(Action action) { try { action(); } catch (Exception error) { message = error.Message; } }
        void OnGUI()
        {
            if (!panel) {
                DrawGuestCommands();
                DrawGuestDialogs();
                if (transport != null) GUI.Box(new Rect(8, 85, 700, 28), "MP " + mode + (authenticated ? " | connected" : " | waiting") + (hostClock==null?"":" | "+hostClock.year+"-"+hostClock.month.ToString("00")+"-"+hostClock.day.ToString("00")+" "+Tools.ConvertFloatToTimestamp(hostClock.hour)+(hostClock.paused?" | PAUSED":" | "+hostClock.speed+"x"))+ " | F9");
                else if(!string.IsNullOrEmpty(message)) GUI.Box(new Rect(8,85,Math.Min(Screen.width-16,950),65), message+" | F9 for details");
                return;
            }
            var oldMatrix = GUI.matrix; var oldColor = GUI.color;
            float scale = Math.Min(1f, Math.Min(Screen.width / 1100f, Screen.height / 740f));
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            GUI.color = new Color(.06f,.09f,.12f,1); GUI.DrawTexture(new Rect(0,0,width,height), white); GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(18, 15, width - 36, height - 30));
            GUILayout.BeginHorizontal(); GUILayout.Label(MpBuild.Label + " | Native campaign-map replication test");
            if (GUILayout.Button("Close (F9 / Esc)", GUILayout.Width(130))) { ClosePanel(); }
            GUILayout.EndHorizontal();
            if (transport == null) {
                GUILayout.Label("Host a loaded campaign. Guests join from the main menu and receive an isolated host checkpoint.");
                GUILayout.Space(12);
                GUILayout.Label("Trusted LAN/VPN only: this prototype uses a session key over unencrypted TCP. No ports or firewall rules are opened automatically.");
                GUILayout.Label("Host: open F9 in a campaign. Guest: open F9 at the main menu and enter the host address and key. The host stays paused during synchronization.");
                GUILayout.Label("IP address (host bind / guest destination). Use 0.0.0.0 to host on LAN; guests use the host's LAN IP."); address = GUILayout.TextField(address, 45);
                GUILayout.Label("TCP port"); port = GUILayout.TextField(port, 5);
                mode="coop";
                GUILayout.Label("Co-op replication test. Movement, HQ regrouping, recruitment, policies, projects and time requests are supported. Complete economy, battles and versus remain incomplete.");
                GUILayout.Label("Session key (guest)"); token = GUILayout.TextField(token, 32);
                GUILayout.BeginHorizontal(); if (GUILayout.Button("Host campaign")) Try(BeginHost); if (GUILayout.Button("Join host")) Try(BeginJoin); GUILayout.EndHorizontal();
            } else if (host) {
                GUILayout.Label("Hosting " + mode + " | Host: " + SideName(hostSide) + " | Guest: " + SideName(guestSide));
                GUILayout.Label("Session key (share with your guest)"); GUILayout.TextField(token);
                GUILayout.Label("Listening address: " + address + ":" + port);
                GUILayout.Label("Close F9 and resume the campaign manually. Host controls time, saving and battle resolution. Guest can select formations and request movement on the native map. Closing a connection pauses the host campaign.");
                if (GUILayout.Button("End session and pause campaign")) End("Session ended. Campaign paused.");
            } else {
                GUILayout.BeginHorizontal(); GUILayout.Label("Guest: " + SideName(guestSide) + " | " + mode + (snapshot == null ? " | connecting" : snapshot.paused ? " | HOST PAUSED" : " | running"));
                if (GUILayout.Button("Disconnect", GUILayout.Width(130))) End("Disconnected."); GUILayout.EndHorizontal();
                GUILayout.Label("Close F9 to view the native map and supported panels. Camera movement remains local. Esc opens the session menu. Left-click selects a formation; right-click terrain requests movement from the host. Other native mutations remain blocked. Disconnect after synchronization returns to the main menu without saving.");
            }
            GUILayout.FlexibleSpace(); GUILayout.Label(message);
            GUILayout.EndArea(); GUI.color = oldColor; GUI.matrix = oldMatrix;
        }
    }
}
