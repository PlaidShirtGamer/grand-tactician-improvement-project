using GTIP;

int checks = 0;
void Check(bool valid, string name)
{
    if (!valid) throw new Exception(name);
    checks++;
    Console.WriteLine("PASS " + name);
}
void Reset()
{
    MultiplayerCampaign.TacticalHolding = true;
    MultiplayerCampaign.TacticalInBattle = true;
    GameVars.frame = 13;
    GameVars.initializeregiments = true;
    GameVars.initializeterrainobjects = true;
    GameVars.allregimentsinitialized = true;
    GameVars.SetWeather(null);
    GameVars.gamepaused = false;
    GameVars.gamespeed = 20;
    BattleUnits.ChiefUpdates = 0;
    BattleController.quitbattlescene = false;
    CampaignController.quitcampaignscene = false;
    Camera_Battlefield.UiUpdated = true;
}

Reset();
MultiplayerCampaign.HoldTacticalClock();
Check(GameVars.gamepaused && GameVars.gamespeed == 0, "Clock hold works before native weather and UI references exist");
var weather = new UniStormWeatherSystem_C();
GameVars.SetWeather(weather);
MultiplayerCampaign.HoldTacticalClock();
Check(weather.timeStopped, "An initialized weather clock is stopped without calling native SetGameSpeed");

foreach (bool guest in new[] { false, true })
{
    Reset();
    MultiplayerCampaign.IsReplica = guest;
    string role = guest ? "Guest" : "Host";
    var regiment = new Regiment();
    Check(MultiplayerCampaign.TryTacticalRegiment(regiment), role + " regiment Update is handled while holding");
    MultiplayerCampaign.TryTacticalRegiment(regiment);
    Check(regiment.Initializations == 1 && regiment.InitializationScoped && !regiment.InitializedAsGarrison,
        role + " native regiment model is initialized once within the initialization permission");
    Check(regiment.SimulationUpdates == 0 && regiment.Men == 1000 && regiment.Ammo == 50,
        role + " paused regiment never enters movement, casualty or ammunition simulation");
    Check(!MultiplayerCampaign.TacticalInitializing, role + " initialization permission expires before other callbacks");

    var units = new BattleUnits();
    GameVars.frame = 12;
    MultiplayerCampaign.TryTacticalUnits(units);
    Check(units.Placements == 0, role + " positioning waits for the staged placement window");
    GameVars.frame = 13;
    MultiplayerCampaign.TryTacticalUnits(units);
    MultiplayerCampaign.TryTacticalUnits(units);
    Check(units.Placements == 1 && units.Transfers == 1 && units.CommanderRankings == 1 && units.UnitRankings == 1,
        role + " tactical construction runs one native placement and cache pass");
    Check(units.InitializationScoped && units.FormSet, role + " tactical placement receives only scoped native cache permission");
    Check(units.SimulationUpdates == 0 && units.ReinforcementArrivals == 0 && units.Autosaves == 0,
        role + " unit construction never runs reinforcement, autosave or general simulation");

    // op8 restores absolute saved positions at frame 29 after native startup placement.
    SceneManagement.currentoperation = 8;
    var restored = new BattleUnits { Position = 713 };
    GameVars.frame = 28;
    MultiplayerCampaign.TryTacticalUnits(restored);
    restored.Position = 713;
    GameVars.frame = 29;
    MultiplayerCampaign.TryTacticalUnits(restored);
    GameVars.frame = 150;
    MultiplayerCampaign.TryTacticalUnits(restored);
    Check(restored.Position == 713 && restored.Placements == 1,
        role + " op8 restored positions survive all post-frame-29 held updates");
    var late = new BattleUnits { Position = 713 };
    GameVars.frame = 29;
    MultiplayerCampaign.TryTacticalUnits(late);
    Check(late.Placements == 0 && late.Position == 713,
        role + " a missed early placement never repositions restored battle state at frame 29");

    MultiplayerCampaign.TacticalInBattle = false;
    var campaign = new BattleUnits { IsCampaign = true };
    GameVars.frame = 24;
    MultiplayerCampaign.TryTacticalUnits(campaign);
    Check(campaign.Adjustments == 1 && campaign.CampaignRankings == 1 && BattleUnits.ChiefUpdates == 1 && campaign.FormSet,
        role + " campaign return constructs hierarchy before frame-25 saved data restore");
    Check(campaign.Placements == 0 && campaign.SimulationUpdates == 0,
        role + " campaign return does not fall through to native campaign simulation");
    var lateCampaign = new BattleUnits { IsCampaign = true, Position = 611 };
    GameVars.frame = 25;
    MultiplayerCampaign.TryTacticalUnits(lateCampaign);
    Check(lateCampaign.Adjustments == 0 && lateCampaign.Position == 611,
        role + " a late campaign update preserves restored hierarchy and position");
    GameVars.frame = 5000;
    MultiplayerCampaign.TryTacticalUnits(lateCampaign);
    Check(lateCampaign.SimulationUpdates == 0 && MultiplayerCampaign.TryTacticalRegiment(regiment),
        role + " existing campaign draining is held even outside the battle scene");

    var ai = new AIBattle();
    var projectile = new Projectile();
    var coy = new Coy();
    var entry = new EntryPoint();
    var collider = new UnityEngine.Collider();
    MultiplayerCampaign.TacticalBattleAi(ai);
    MultiplayerCampaign.TacticalProjectile(projectile);
    MultiplayerCampaign.TacticalCoyEnter(coy, collider);
    MultiplayerCampaign.TacticalCoyExit(coy, collider);
    MultiplayerCampaign.TacticalEntryPointEnter(entry, collider);
    Check(ai.Updates == 0 && projectile.Updates == 0 && coy.Enters == 0 && coy.Exits == 0 && entry.Enters == 0,
        role + " tactical AI, projectile aging and physics-trigger mutations are blocked");
    Check(!MultiplayerCampaign.TacticalFireBullet(regiment, 17) && regiment.Ammo == 50 && regiment.Bullets == 0,
        role + " direct firing entry cannot spawn effects or spend ammunition while held");
    var building = new Building();
    var group = new ArmyGroup();
    var front = new BattleFront();
    var battlefield = new BattlefieldSetup();
    var climate = new Weather();
    var storm = new UniStormWeatherSystem_C();
    MultiplayerCampaign.TacticalBuilding(building);
    MultiplayerCampaign.TacticalArmyGroup(group);
    MultiplayerCampaign.TacticalBattleFront(front);
    MultiplayerCampaign.TacticalBattlefieldSetup(battlefield);
    MultiplayerCampaign.TacticalWeather(climate);
    MultiplayerCampaign.TacticalStorm(storm);
    Check(building.Updates == 0 && group.Updates == 0 && front.Updates == 0 && climate.Updates == 0 && storm.Updates == 0,
        role + " ownership, destruction, hierarchy and weather callbacks cannot independently mutate a held scene");
    Check(battlefield.Updates == 0,
        role + " battlefield occupation cannot mutate the held campaign");
}

Reset();
var battleController = new BattleController();
GameVars.frame = 1;
MultiplayerCampaign.TacticalInBattle = false;
BattleController.quitbattlescene = true;
MultiplayerCampaign.TacticalBattleController(battleController);
Check(battleController.Updates == 1 && battleController.InitializationScoped && !battleController.SawQuit,
    "Battle first-frame initialization works before a fresh battle UI is observable");
Check(GameVars.gamepaused && GameVars.gamespeed == 0 && !BattleController.quitbattlescene,
    "Battle initialization cannot leave its native clock resumed or native exit flag set");
GameVars.frame = 40;
MultiplayerCampaign.TacticalBattleController(battleController);
Check(battleController.Updates == 2, "Final battle loading frame 40 is permitted");
GameVars.frame = 41;
BattleController.quitbattlescene = true;
MultiplayerCampaign.TacticalBattleController(battleController);
Check(battleController.Updates == 2 && !BattleController.quitbattlescene,
    "Post-initialization battle Update and native result-generating exit are blocked");

var campaignController = new CampaignController();
GameVars.frame = 51;
CampaignController.quitcampaignscene = true;
MultiplayerCampaign.TryTacticalCampaign(campaignController);
Check(campaignController.Updates == 1 && campaignController.InitializationScoped && !campaignController.SawQuit,
    "Campaign loading completes its frame-51 stage within native initialization permission");
GameVars.frame = 52;
CampaignController.quitcampaignscene = true;
MultiplayerCampaign.TryTacticalCampaign(campaignController);
Check(campaignController.Updates == 1 && !CampaignController.quitcampaignscene,
    "Post-load campaign Update and native quit are blocked on both peers");

Reset();
var failedRegiment = new Regiment { FailInitialization = true };
bool failed = false;
try { MultiplayerCampaign.TryTacticalRegiment(failedRegiment); }
catch (InvalidOperationException) { failed = true; }
Check(failed && !MultiplayerCampaign.TacticalInitializing && GameVars.gamepaused && GameVars.gamespeed == 0,
    "A native initialization failure propagates while revoking permission and restoring the hold");
var failedController = new BattleController { Fail = true };
failed = false;
try { MultiplayerCampaign.TacticalBattleController(failedController); }
catch (InvalidOperationException) { failed = true; }
Check(failed && !MultiplayerCampaign.TacticalInitializing && !BattleController.quitbattlescene && GameVars.gamepaused,
    "Controller failures cannot strand a native exit flag or initialization permission");

Reset();
var objective = new Objectives();
GameVars.initializeterrainobjects = false;
MultiplayerCampaign.TacticalObjectives(objective);
Check(objective.Initializations == 0 && objective.TextUpdates == 0, "Objective presentation waits for terrain initialization");
GameVars.initializeterrainobjects = true;
MultiplayerCampaign.TacticalObjectives(objective);
MultiplayerCampaign.TacticalObjectives(objective);
Check(objective.Initializations == 1 && objective.InitializationScoped && objective.TextUpdates == 2 && objective.ScaleUpdates == 2,
    "Objectives construct once and continue local display refreshes");
Check(objective.UiPositions == 2 && objective.Visibilities == 2 && objective.SimulationUpdates == 0 && objective.Owner == 1 && objective.Points == 600,
    "Objective display refresh cannot advance timers, occupation or points");
Camera_Battlefield.UiUpdated = false;
MultiplayerCampaign.TacticalObjectives(objective);
Check(objective.UiPositions == 2 && objective.Visibilities == 2, "Unchanged camera avoids redundant objective positioning");

var camera = new Camera_Battlefield { preventmanualzoom = true, preventmanualrotation = true };
GameVars.frame = 40;
MultiplayerCampaign.TacticalCamera(camera);
Check(camera.Updates == 1 && camera.preventmanualzoom && camera.preventmanualrotation,
    "Local camera is available without overriding briefing locks before loading completes");
GameVars.frame = 41;
MultiplayerCampaign.TacticalCamera(camera);
Check(camera.Updates == 2 && !camera.preventmanualzoom && !camera.preventmanualrotation,
    "Loaded held battlefield permits local camera pan, zoom and rotation");

Reset();
MultiplayerCampaign.TacticalHolding = false;
Check(!MultiplayerCampaign.TryTacticalRegiment(new Regiment()) && !MultiplayerCampaign.TryTacticalUnits(new BattleUnits()) &&
    !MultiplayerCampaign.TryTacticalCampaign(new CampaignController()), "Offline and normal campaign wrappers retain their existing authority path");
var normalBattle = new BattleController();
MultiplayerCampaign.TacticalBattleController(normalBattle);
Check(normalBattle.Updates == 1 && !normalBattle.InitializationScoped && !GameVars.gamepaused,
    "Outside a tactical hold the battle controller keeps native behavior");
var normalAi = new AIBattle();
var normalProjectile = new Projectile();
var normalRegiment = new Regiment();
var normalObjective = new Objectives();
var normalCoy = new Coy();
var normalEntry = new EntryPoint();
var originalCollider = new UnityEngine.Collider();
MultiplayerCampaign.TacticalBattleAi(normalAi);
MultiplayerCampaign.TacticalProjectile(normalProjectile);
bool fired = MultiplayerCampaign.TacticalFireBullet(normalRegiment, 47);
MultiplayerCampaign.TacticalObjectives(normalObjective);
MultiplayerCampaign.TacticalCoyEnter(normalCoy, originalCollider);
MultiplayerCampaign.TacticalCoyExit(normalCoy, originalCollider);
MultiplayerCampaign.TacticalEntryPointEnter(normalEntry, originalCollider);
Check(normalAi.Updates == 1 && normalProjectile.Updates == 1 && normalObjective.SimulationUpdates == 1,
    "Outside a tactical hold AI, projectiles and objectives use preserved native methods");
Check(fired && normalRegiment.LastSprite == 47 && normalRegiment.Ammo == 49,
    "Native firing preserves its sprite argument, return value and ammunition behavior");
Check(normalCoy.Enters == 1 && normalCoy.Exits == 1 && normalEntry.Enters == 1 &&
    ReferenceEquals(normalCoy.LastCollider, originalCollider) && ReferenceEquals(normalEntry.LastCollider, originalCollider),
    "Native trigger callbacks receive the original collider argument");
var normalBuilding = new Building();
var normalGroup = new ArmyGroup();
var normalFront = new BattleFront();
var normalBattlefield = new BattlefieldSetup();
var normalClimate = new Weather();
var normalStorm = new UniStormWeatherSystem_C();
MultiplayerCampaign.TacticalBuilding(normalBuilding);
MultiplayerCampaign.TacticalArmyGroup(normalGroup);
MultiplayerCampaign.TacticalBattleFront(normalFront);
MultiplayerCampaign.TacticalBattlefieldSetup(normalBattlefield);
MultiplayerCampaign.TacticalWeather(normalClimate);
MultiplayerCampaign.TacticalStorm(normalStorm);
Check(normalBuilding.Updates == 1 && normalGroup.Updates == 1 && normalFront.Updates == 1 && normalClimate.Updates == 1 && normalStorm.Updates == 1,
    "Outside a tactical hold buildings, groups, battlefronts and both weather systems keep native Update behavior");
Check(normalBattlefield.Updates == 1,
    "Outside a tactical hold battlefield setup keeps native Update behavior");
Console.WriteLine("Tactical authority: " + checks + " checks passed.");

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        public static bool TacticalHolding, TacticalInBattle, IsReplica;
    }
}
namespace UnityEngine { public sealed class Collider { } }
// Private native fields are deliberately accessed through the production reflection bridge.
#pragma warning disable CS0414, CS0649
public sealed class UniStormWeatherSystem_C
{
    public bool timeStopped;
    public int Updates;
    void GTIP_Original_TacticalStorm() { Updates++; }
}
public static class GameVars
{
    public static bool gamepaused, initializeregiments, initializeterrainobjects, allregimentsinitialized;
    public static float gamespeed;
    public static int frame;
    static UniStormWeatherSystem_C uniStormSystem;
    public static void SetWeather(UniStormWeatherSystem_C value) { uniStormSystem = value; }
}
public static class SceneManagement { public static int currentoperation; }
public sealed class Regiment
{
    bool initialized;
    public int Initializations, SimulationUpdates, Men = 1000, Ammo = 50, Bullets, LastSprite;
    public bool InitializationScoped, InitializedAsGarrison, FailInitialization;
    void Initialize(bool isgarrison = false)
    {
        Initializations++;
        InitializationScoped = MultiplayerCampaign.TacticalInitializing;
        InitializedAsGarrison = isgarrison;
        GameVars.gamepaused = false;
        if (FailInitialization) throw new InvalidOperationException("native model failed");
        initialized = true;
    }
    void GTIP_Original_ReplicaRegiment() { SimulationUpdates++; Men--; Ammo--; }
    bool GTIP_Original_TacticalFireBullet(int spriteid) { LastSprite = spriteid; Ammo--; Bullets++; return true; }
}
public sealed class BattleUnits
{
    bool initiated = true, formset;
    public bool IsCampaign, InitializationScoped;
    public int Placements, Transfers, CommanderRankings, UnitRankings, Adjustments, CampaignRankings;
    public int SimulationUpdates, ReinforcementArrivals, Autosaves, Position;
    public static int ChiefUpdates;
    public bool FormSet => formset;
    void DoUnitPositioning() { Placements++; Position = 33; InitializationScoped = MultiplayerCampaign.TacticalInitializing; }
    void TransferVariablesToSubordinates() { Transfers++; }
    void UpdateCommanderRankings(bool overridelastupdate = false) { if (!overridelastupdate) throw new Exception("ranking must initialize"); CommanderRankings++; }
    void UpdateUnitRankings(bool overridelastupdate = false) { if (!overridelastupdate) throw new Exception("ranking must initialize"); UnitRankings++; }
    void AdjustArmyStructureToCampaign() { Adjustments++; }
    void UpdateCommanderRankingsCampaign(int commanderid = -1) { if (commanderid != -1) throw new Exception("native default changed"); CampaignRankings++; }
    static void UpdateCommanderInChief(bool forceoverride = false) { ChiefUpdates++; }
    void GTIP_Original_ReplicaUnits() { SimulationUpdates++; ReinforcementArrivals++; Autosaves++; }
}
public sealed class BattleController
{
    public static bool quitbattlescene;
    public int Updates;
    public bool InitializationScoped, SawQuit, Fail;
    void GTIP_Original_TacticalBattleController()
    {
        Updates++;
        InitializationScoped = MultiplayerCampaign.TacticalInitializing;
        SawQuit = quitbattlescene;
        quitbattlescene = true;
        GameVars.gamespeed = 1;
        GameVars.gamepaused = false;
        if (Fail) throw new InvalidOperationException("native controller failed");
    }
}
public sealed class CampaignController
{
    public static bool quitcampaignscene;
    public int Updates;
    public bool InitializationScoped, SawQuit;
    void GTIP_Original_ReplicaCampaign()
    {
        Updates++;
        InitializationScoped = MultiplayerCampaign.TacticalInitializing;
        SawQuit = quitcampaignscene;
        quitcampaignscene = true;
        GameVars.gamespeed = 1;
        GameVars.gamepaused = false;
    }
}
public sealed class AIBattle { public int Updates; void GTIP_Original_TacticalBattleAi() { Updates++; } }
public sealed class Projectile { public int Updates; void GTIP_Original_TacticalProjectile() { Updates++; } }
public sealed class Building { public int Updates; void GTIP_Original_TacticalBuilding() { Updates++; } }
public sealed class ArmyGroup { public int Updates; void GTIP_Original_TacticalArmyGroup() { Updates++; } }
public sealed class BattleFront { public int Updates; void GTIP_Original_TacticalBattleFront() { Updates++; } }
public sealed class BattlefieldSetup { public int Updates; void GTIP_Original_TacticalBattlefieldSetup() { Updates++; } }
public sealed class Weather { public int Updates; void GTIP_Original_TacticalWeather() { Updates++; } }
public sealed class Objectives
{
    bool initialized;
    public bool InitializationScoped;
    public int Initializations, TextUpdates, ScaleUpdates, UiPositions, Visibilities, SimulationUpdates, Owner = 1, Points = 600;
    void Initialize() { Initializations++; InitializationScoped = MultiplayerCampaign.TacticalInitializing; initialized = true; }
    void UpdateTextValues() { TextUpdates++; }
    void UpdateUIScale() { ScaleUpdates++; }
    void UpdateUIPosition() { UiPositions++; }
    void UpdateTextVisibility() { Visibilities++; }
    void GTIP_Original_TacticalObjectives() { SimulationUpdates++; Owner = 0; Points++; }
}
public sealed class Coy
{
    public int Enters, Exits;
    public UnityEngine.Collider LastCollider;
    void GTIP_Original_TacticalCoyEnter(UnityEngine.Collider other) { Enters++; LastCollider = other; }
    void GTIP_Original_TacticalCoyExit(UnityEngine.Collider other) { Exits++; LastCollider = other; }
}
public sealed class EntryPoint
{
    public int Enters;
    public UnityEngine.Collider LastCollider;
    void GTIP_Original_TacticalEntryPointEnter(UnityEngine.Collider other) { Enters++; LastCollider = other; }
}
public sealed class Camera_Battlefield
{
    public bool preventmanualzoom, preventmanualrotation;
    public static bool UiUpdated;
    public int Updates;
    public static bool IsCamUIUpdated() => UiUpdated;
    void GTIP_Original_EditorCamera() { Updates++; }
}
