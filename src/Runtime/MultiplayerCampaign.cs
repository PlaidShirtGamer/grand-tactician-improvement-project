using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using UnityEngine;

namespace GTIP
{
    // Campaign command-client prototype: only the host owns a running campaign.
    public sealed partial class MultiplayerCampaign : MonoBehaviour
    {
        static MultiplayerCampaign instance;
        static int suppressInputUntilFrame = -1;
        public static bool BlocksInput => (instance != null && (instance.panel || instance.GuestDialogOpen)) || Time.frameCount <= suppressInputUntilFrame;
        public static bool HoldsCampaignAi => instance != null && instance.transport != null && instance.host && (instance.mode == "versus" || instance.phase == "sending");
        MpTransport transport;
        BattleUI ui;
        GameObject root;
        object officers;
        bool host, authenticated, helloSent, panel, mainMenu, previousBackground, loggedSnapshot;
        string diagnosticPath, rejection;
        float rejectionAt;
        int hostSide, guestSide, lastSequence, nextId;
        string session, token = "", mode = "coop", address = "127.0.0.1", port = "18615", message = "";
        float lastSend, connectedAt;
        readonly Dictionary<Regiment, int> ids = new Dictionary<Regiment, int>();
        readonly Dictionary<int, Regiment> units = new Dictionary<int, Regiment>();
        MpMessage snapshot;
        public static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("GTIP_Multiplayer"); DontDestroyOnLoad(go);
            instance = go.AddComponent<MultiplayerCampaign>();
        }
        public static void Observe(BattleUI target) { if (instance != null) instance.ui = target; }
        public static void MenuChanged(bool visible) { if (instance != null) instance.mainMenu = visible; }
        bool Ready => ui != null && ui.IsCampaign && !ui.IsMainMenu && GameVars.frame > 50 && GameVars.allregimentsinitialized &&
            BattleUnits.unitfolder != null && SixLevelRuntime.Active && !DLC_WL.dlc_scenarioactive;
        void BeginHost()
        {
            if (!Ready) throw new InvalidOperationException("Load a main campaign before hosting.");
            if (mode != "coop") throw new InvalidOperationException("Native-map milestone currently supports co-op observation only.");
            if (GameVars.ai_vs_ai) throw new InvalidOperationException("Disable AI-versus-AI before hosting.");
            hostSide = GameVars.playeralliance; guestSide = MpRules.GuestSide(mode, hostSide);
            GameVars.SetGameSpeed(0, manualset: true);
            BuildManifest();
            var listener = MpTransport.Host(IPAddress.Parse(address), ParsePort());
            transport = listener; host = true; authenticated = false; helloSent = false;
            StartDiagnostics();
            session = Guid.NewGuid().ToString("N"); token = Guid.NewGuid().ToString("N");
            root = BattleUnits.unitfolder; officers = GameVars.commander; lastSequence = 0;
            phase="waiting"; bootstrapStarted=Time.realtimeSinceStartup;
            message = "Waiting for one guest. Share the address, port and session key. Campaign remains paused.";
        }
        int ParsePort() { if (!int.TryParse(port, out int value) || value < 1024 || value > 65535) throw new InvalidOperationException("Use a port from 1024 to 65535."); return value; }
        void BeginJoin()
        {
            if (ui==null || !ui.IsMainMenu) throw new InvalidOperationException("Return to the main menu to join a host campaign.");
            if(token.Length!=32) throw new InvalidOperationException("Enter the host's session key.");
            host=false; authenticated=false; helloSent=false; replicaReady=false; phase="joining";
            bootstrapStarted=Time.realtimeSinceStartup;
            transport=MpTransport.Join(IPAddress.Parse(address),ParsePort());
            StartDiagnostics(); snapshot=null; message="Connecting to receive host campaign...";
        }
        void End(string reason)
        {
            bool wasHost = transport != null && host;
            bool exitReplica=transport!=null && !host && hasLiveData && root==BattleUnits.unitfolder;
            if (transport != null) { LogDiagnostic(reason); Application.runInBackground = previousBackground; }
            DestroyRecruitPreview();previewPanel=null;previewCommander=-1;
            transport?.Dispose(); transport = null; authenticated = false; helloSent = false; snapshot = null;
            ClearData(false);
            newsBarrier.Clear();newspaperState=new MpNewspaperState();guestNewsSerial=0;guestNewsClosed=false;guestFileMenu=false;
            financeViews.Clear();replicaAlerts.Clear();alertPositions.Clear();
            clockSequence=receivedClockSequence=0;
            if(exitReplica) {
                // Deleted native objects cannot be rolled back to the join checkpoint in place.
                // Leave without saving; a reconnect obtains the current authoritative checkpoint.
                originalPositions.Clear();originalRotations.Clear();originalActive.Clear();originalParents.Clear();originalSiblingIndices.Clear();restoreClock=null;hostClock=null;
                SceneManagement.batchoperation.Add(new SceneManagement.BatchOperation(SceneManagement.bo_showblackscreen));
                SceneManagement.batchoperation.Add(new SceneManagement.BatchOperation(SceneManagement.bo_exitcampaignscenario,1));
                SceneManagement.batchoperation.Add(new SceneManagement.BatchOperation(SceneManagement.bo_rebuildscene,1));
                SceneManagement.batchoperation.Add(new SceneManagement.BatchOperation(SceneManagement.bo_showmainmenu,2));
                SceneManagement.batchoperation.Add(new SceneManagement.BatchOperation(SceneManagement.bo_hideblackscreen,2));
            } else RestoreReplicaState();
            ClearBootstrap();leavingReplica=exitReplica;hasLiveData=false;guestSelection=null;guestSequence=0;choosingCommander=false;orderStatus="";ClosePanel();
            ids.Clear(); units.Clear(); session = null; message = reason; rejection = null;
            if (wasHost && Ready) GameVars.SetGameSpeed(0, manualset: true);
        }
        void StartDiagnostics()
        {
            previousBackground = Application.runInBackground; Application.runInBackground = true;
            loggedSnapshot = false; diagnosticPath = null;
            try {
                string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "GTIP-Logs"); Directory.CreateDirectory(folder);
                diagnosticPath = Path.Combine(folder, "mp-" + (host ? "host-" : "guest-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".log");
                LogDiagnostic(MpBuild.Label + " protocol " + MpRules.Protocol + "; background updates enabled.");
                LogHotkeys();
                LogDiagnostic("Checkpoint: faction="+GameVars.playeralliance+", date="+checkpointDate.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+", formations="+(manifestKeys==null?0:manifestKeys.Length)+", identity hash="+manifest);
                if(diagnosticPath!=null && manifestKeys!=null) File.WriteAllLines(diagnosticPath+".formations.txt",manifestKeys);
            } catch (Exception error) { Debug.LogWarning("[GTIP MP] Could not create diagnostic log: " + error.Message); }
        }
        void LogDiagnostic(string text)
        {
            // Never log session keys or complete packets. Each install has its own log directory.
            if (diagnosticPath == null) return;
            try { File.AppendAllText(diagnosticPath, DateTime.UtcNow.ToString("O") + " " + text + Environment.NewLine); }
            catch { diagnosticPath = null; }
        }
        void OnDestroy() { End("Disconnected."); RestoreInput(); if (white != null) Destroy(white); if (instance == this) instance = null; }
        void Update()
        {
            if(leavingReplica && (root!=BattleUnits.unitfolder || (ui!=null && ui.IsMainMenu))){leavingReplica=false;RestoreInput();}
            if (Input.GetKeyDown(KeyCode.Escape) && panel) {
                if(panel) ClosePanel();
                else {
                    panel=true; BlockInput(); message="Session menu. Connection remains active; use Disconnect to leave.";

                }
                return;
            }
            if (Input.GetKeyDown(KeyCode.F9)) {
                if (!panel && !CampaignEditor.BlocksInput) { panel = true; BlockInput(); if (Ready) GameVars.SetGameSpeed(0, manualset: true); }
                else { ClosePanel(); }
            }
            if (transport == null) return;
            try {
                if (host && (!Ready || root != BattleUnits.unitfolder || !ReferenceEquals(officers, GameVars.commander) || hostSide != GameVars.playeralliance))
                { End("Campaign changed or entered a tactical battle. Multiplayer session ended."); return; }
                if (!host && phase=="live" && (!Ready || root!=BattleUnits.unitfolder || !ReferenceEquals(officers,GameVars.commander))) { End("Guest campaign changed. Session ended."); return; }
                // Drain delivered messages before observing EOF, so a rejection is not lost.
                int count = 0;
                while (transport != null && count++ < 8 && transport.Receive(out var payload)) Handle(MpCodec.Decode(payload));
                if (transport == null) return;
                if (rejection != null) {
                    if (transport.Closed || Time.realtimeSinceStartup-rejectionAt>3) End("Session rejected: "+rejection);
                    return;
                }
                if (transport.Closed) { End("Disconnected: " + (transport.Failure ?? "peer closed connection") + ". Host campaign paused."); return; }
                if (!transport.Connected) return;
                if (!helloSent) {
                    connectedAt = Time.realtimeSinceStartup; helloSent = true;
                    if (!host) Send(new MpMessage { kind = "join", token = token, text = MpBuild.Version });
                }
                if(phase!="live") { BootstrapTick(); return; }
                DataTick();
                if(!host) {GuestMapInput();GuestTimeKeys();}
                if (authenticated && Time.realtimeSinceStartup - lastSend > 1) {
                    lastSend = Time.realtimeSinceStartup;
                    if(!host) Send(new MpMessage { kind = "ping", session = session });
                    else if(outgoingData==null && prepareTask==null && commitTask==null && !RosterChanged()) Send(Capture());
                }
            } catch (Exception error) { LogDiagnostic(error.ToString()); Debug.LogWarning("[GTIP MP] " + error); End("Session stopped: " + error.Message); }
        }
        void Send(MpMessage value)
        {
            value.protocol = MpRules.Protocol;
            string payload = MpCodec.Encode(value);
            if (value.kind == "snapshot" && !loggedSnapshot) {
                MpRules.ValidateSnapshot(MpCodec.Decode(payload));
                LogDiagnostic("First snapshot encoded and verified: units=" + value.units.Length + ", towns=" + value.towns.Length + ", bytes=" + payload.Length);
                loggedSnapshot = true;
            }
            transport?.Send(payload);
        }
        void RejectGuest(string reason)
        {
            rejection=reason; rejectionAt=Time.realtimeSinceStartup;
            message="Session rejected: "+reason;
            LogDiagnostic(message);
            Send(new MpMessage { kind="reject", text=reason });
        }
        void Handle(MpMessage value)
        {
            if (value == null || value.protocol != MpRules.Protocol) throw new InvalidOperationException("Protocol mismatch.");
            if(BootstrapMessage(value)) return;
            if (!authenticated) {
                if (host) {
                    RejectGuest("Unexpected message before checkpoint synchronization.");
                } else {
                    if(value.kind=="reject") { End("Host rejected connection: "+value.text); return; }
                    if (value.kind != "welcome" || string.IsNullOrEmpty(value.session) || value.text != MpBuild.Version || value.side < 0 || value.side > 1 || (value.mode != "coop" && value.mode != "versus"))
                        throw new InvalidOperationException("Invalid host handshake.");
                    session = value.session; guestSide = value.side; mode = value.mode; authenticated = true; phase="live"; message = "Connected. Waiting for campaign snapshot.";
                    if(value.campaign!=manifest || guestSide!=GameVars.playeralliance || mode!="coop") throw new InvalidOperationException("Native-map checkpoint/faction mismatch.");
                    ApplyHierarchy(value.units);
                }
                return;
            }
            if (value.session != session) throw new InvalidOperationException("Session mismatch.");
            if(DataMessage(value)) return;
            if(NewspaperMessage(value))return;
            if (host) {
                if (value.kind == "ping") return;
                try { Execute(value); LogDiagnostic("Guest order "+value.sequence+" accepted: "+value.kind+" formation="+value.unit); Send(new MpMessage { kind = "result", session = session, sequence = value.sequence, text = "Order submitted to native campaign rules. Readiness, terrain and order delay still apply." }); }
                catch (Exception error) { LogDiagnostic("Guest order "+value.sequence+" rejected: "+error.Message); Send(new MpMessage { kind = "result", session = session, sequence = value.sequence, text = "Order rejected: " + error.Message }); }
            } else if(value.kind=="clock") {
                if(!MpRules.NewClock(value.sequence,receivedClockSequence))return;
                receivedClockSequence=value.sequence;
                ApplyClock(value.clock);
            } else if (value.kind == "hierarchy") {
                ApplyHierarchy(value.units);
            } else if (value.kind == "snapshot") {
                if(value.sequence<incomingDataSerial)return;
                if(value.sequence>incomingDataSerial)throw new InvalidOperationException("Position update precedes campaign data generation.");
                MpRules.ValidateSnapshot(value);
                if (!loggedSnapshot) { LogDiagnostic("First snapshot accepted: units=" + value.units.Length + ", towns=" + value.towns.Length); loggedSnapshot = true; }
                snapshot = value;
                if(hostClock==null)ApplyClock(value.clock); // only the sequenced clock stream advances time
                ApplyReplica(value);
            } else if (value.kind == "result") { orderStatus = "Order " + value.sequence + ": " + value.text; LogDiagnostic(orderStatus); }
            else throw new InvalidOperationException("Unexpected message.");
        }
        int Id(Regiment unit)
        {
            if (unit == null) return 0;
            if (!ids.TryGetValue(unit, out int value)) { value = ++nextId; ids[unit] = value; units[value] = unit; }
            return value;
        }
        bool Controllable(Regiment unit) => unit != null && unit.alliance == guestSide && unit.gameObject.activeInHierarchy &&
            BattleUnits.GetUnitVisibilityStateOnCampaignMap(unit) > 0 && unit.unittyp != 17 && unit.garrisonreference == null && !unit.inbattle && !unit.onretreat && !unit.isrouted;
        bool Visible(Regiment unit)
        {
            if (unit.alliance == guestSide) return true;
            if (!GameVars.usefow) return true;
            // iswithinfog is relative to the host. Query the observer's FOW explicitly for versus.
            try {
                var fog = unit.fows == null || unit.fows.Length < 2 ? null : unit.fows[guestSide == hostSide ? 0 : 1];
                if (fog == null) return false;
                float cunning = unit.commander >= 0 && unit.commander < GameVars.commander.Count ? GameVars.commander[unit.commander].GetCommanderCunning() : -1;
                int type = unit.unittyp == HierarchyModel.Army ? HierarchyModel.Corps : unit.unittyp;
                return !fog.UnitIsHidden(unit.GetPosition(), cunning, (float)unit.strength / Math.Max(1, GamePrefs.unittypesize[type]), unit.liedown, unit.coverobject, unit.covervalue, type, unit);
            } catch { return false; } // Missing fog information must not reveal opponents.
        }
        MpMessage Capture()
        {
            var rows = new List<MpUnit>();
            if(BattleUnits.completeunitlist.Count(u=>u!=null)!=ids.Count || BattleUnits.completeunitlist.Any(u=>u!=null && !ids.ContainsKey(u)))
                throw new InvalidOperationException("Formation roster changed. Save a new checkpoint and reload it on both clients before reconnecting.");
            foreach (var unit in BattleUnits.completeunitlist) {
                if (unit == null || !Visible(unit)) continue;
                bool own = unit.alliance == guestSide; var pos = unit.transform.position;
                if (!MpRules.Finite(pos.x) || !MpRules.Finite(pos.y) || !MpRules.Finite(pos.z)) continue;
                var target = own && unit.regimentpaths > 0 ? unit.GetLastWaypointPos() : pos;
                if (!MpRules.Finite(target.x) || !MpRules.Finite(target.z)) target = pos;
                rows.Add(new MpUnit { id = Id(unit), side = unit.alliance, tier = unit.unittyp,
                    name = unit.name, commander = own ? CampaignEditorData.OfficerName(unit.commander) : "",
                    men = own ? unit.GetComponentsInChildren<Regiment>().Where(r => r.unittyp <= 2).Sum(r => r.strength) : -1,
                    x = pos.x, y=pos.y, z = pos.z, yaw=unit.transform.eulerAngles.y, targetX = target.x, targetZ = target.z,
                    controllable = Controllable(unit), moving = own && unit.regimentpaths > 0 });
            }
            var result = new MpMessage { kind = "snapshot", session = session, side = guestSide, mode = mode, campaign=manifest, sequence=dataSerial,
                date = GameVars.GetCampaignFloatingDate(), paused = GameVars.gamepaused, clock=CaptureClock(), units = rows.ToArray(),
                towns = (BattleUnits.towns ?? new List<Town>()).Where(t => t != null).Select(t => new MpTown { name = t.CityName, x = t.transform.position.x, z = t.transform.position.z })
                    .Where(t => MpRules.Finite(t.x) && MpRules.Finite(t.z)).ToArray() };
            MpRules.ValidateSnapshot(result);
            return result;
        }
        void Execute(MpMessage value)
        {
            if (!Ready || panel || CampaignEditor.BlocksInput) throw new InvalidOperationException("Host is editing the campaign or session is not ready.");
            if(ExecuteGlobal(value))return;
            if (!units.TryGetValue(value.unit, out var unit) || unit == null || !BattleUnits.completeunitlist.Contains(unit)) throw new InvalidOperationException("Formation no longer exists.");
            MpRules.Authorize(value, session, guestSide, unit.alliance, Controllable(unit), lastSequence);
            lastSequence = value.sequence;
            if(value.kind=="regroup") {
                if(value.x<=0 || value.x!=(int)value.x || !units.TryGetValue((int)value.x,out var parent) || parent==null || parent.alliance!=guestSide)
                    throw new InvalidOperationException("Destination headquarters no longer exists or belongs to another faction.");
                EditorFormationOperations.Move(unit,parent);return;
            }
            if(value.kind=="commander") {
                if(value.x<0 || value.x>=GameVars.commander.Count || value.x!=(int)value.x)throw new InvalidOperationException("Unknown officer.");
                if(value.text!=OfficerCommandIdentity((int)value.x))throw new InvalidOperationException("Officer identity changed; reopen the commander list.");
                EditorFormationOperations.ReplaceCommander(unit,(int)value.x);return;
            }
            if (value.kind == "stop") { unit.StopRegiment(manualstop: true); return; }
            var terrain = Terrain.activeTerrain;
            if (terrain == null) throw new InvalidOperationException("Campaign terrain is unavailable.");
            var origin = terrain.transform.position; var size = terrain.terrainData.size;
            if (value.x < origin.x || value.x > origin.x + size.x || value.z < origin.z || value.z > origin.z + size.z)
                throw new InvalidOperationException("Destination is outside the campaign map.");
            var point = new Vector3(value.x, 0, value.z); point.y = terrain.SampleHeight(point) + origin.y;
            var controller = GameObject.Find("GameController").GetComponent<BattleUnits>();
            controller.SetWaypoint(unit, point, useorderdelay: true, showmovementoptions: false);
        }
        static string SideName(int side) => side == 0 ? "Union" : "Confederacy";
    }
}
