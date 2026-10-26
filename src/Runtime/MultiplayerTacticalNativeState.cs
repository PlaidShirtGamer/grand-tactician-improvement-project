using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public static class MpTacticalNativeState
    {
        public static string Capture(bool campaign) => MpTacticalFingerprint.Hash(CaptureWorld(campaign));
        public static MpTacticalFingerprintRecord[] CaptureWorld(bool campaign)
        {
            var controller=GameObject.Find("GameController")?.GetComponent<BattleUnits>();
            if(controller==null || controller.IsCampaign!=campaign || !GameVars.gamepaused || !GameVars.allregimentsinitialized || GameVars.frame<=50 ||
                BattleUnits.unitfolder==null || controller.uniStormSystem==null)
                throw new InvalidOperationException("The paused native world has not finished initialization.");
            var native=BattleUnits.unitfolder.GetComponentsInChildren<Regiment>(true);
            if(native.Length==0)throw new InvalidDataException("Initialized native world has no units.");
            var keys=new Dictionary<Regiment,string>();var building=new HashSet<Regiment>();
            string Key(Regiment unit)
            {
                if(unit==null)return "";
                if(keys.TryGetValue(unit,out string existing))return existing;
                if(!building.Add(unit) || building.Count>64)throw new InvalidDataException("Cyclic initialized native hierarchy.");
                Regiment parent=Parent(unit);string parentKey=parent==null?"":Key(parent);
                string segment=MpTacticalFingerprint.Segment(unit.alliance,unit.unittyp,unit.commander,unit.name);
                string result=parentKey.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+parentKey+segment;
                building.Remove(unit);keys.Add(unit,result);return result;
            }
            foreach(var unit in native)Key(unit);
            if(keys.Count!=native.Length || keys.Values.Distinct(StringComparer.Ordinal).Count()!=keys.Count)
                throw new InvalidDataException("Ambiguous initialized native unit identities.");
            string Reference(Regiment unit)
            {if(unit==null)return "";if(!keys.TryGetValue(unit,out var key))throw new InvalidDataException("Initialized native reference leaves the loaded world.");return key;}
            var records=new List<MpTacticalFingerprintRecord>();var clock=controller.uniStormSystem;
            records.Add(new MpTacticalFingerprintRecord{Kind="world",Key="context",Parent="",Text=new[]{SceneManagement.currentcampaignscenario??"",campaign?"":GamePrefs.leveltoload+"/"+GamePrefs.subleveltoload},
                Integers=new[]{campaign?1:0,GameVars.playeralliance,controller.year,clock.monthCounter,clock.dayCounter,campaign?0:controller.battlepasseddays},
                Flags=new[]{GameVars.gamepaused},Values=new[]{clock.Hour,GameVars.currenttimefromstart,campaign?0f:controller.battletime}});
            foreach(var unit in native) {
                if(unit.gameObject.activeInHierarchy && !NativeAccess.Get<bool>(unit,"initialized"))throw new InvalidDataException("An active native unit is not initialized: "+unit.name);
                string key=keys[unit];Regiment parent=Parent(unit);
                var parentRegiment=unit.parentregiment==null?null:unit.parentregiment.GetComponent<Regiment>();
                if(unit.parentregiment!=null && parentRegiment==null)throw new InvalidDataException("Initialized parent-regiment reference has no native unit.");
                // Field ordering is schema1: identity/type/force/equipment, saved
                // tactical orders/state, then saved condition and ammunition.
                records.Add(new MpTacticalFingerprintRecord{Kind="unit",Key=key,Parent=Reference(parent),Text=new[]{unit.name,unit.abrvname??"",Reference(parentRegiment)},
                    Integers=new[]{unit.alliance,unit.unittyp,unit.commander,unit.nation,unit.strength,unit.sick,unit.guns,unit.weapon,unit.formation,unit.groupformation,unit.formationordered,
                        unit.mounted,unit.surrenderstate,unit.combatbehavior,unit.combatbehaviorordered,unit.unitstatus,unit.blockobjects,unit.blockobjectsused,unit.regimentpaths,unit.movementmode},
                    Flags=new[]{unit.gameObject.activeSelf,unit.gameObject.activeInHierarchy,unit.detached,unit.permanentlydetached,unit.isrouted,unit.running,unit.doublequick,unit.pathinterrupted},
                    Values=new[]{unit.timeofarrival,unit.morale,unit.fatigue,unit.training,unit.experience,unit.cohesion,unit.conditionmen,unit.conditionhorses,unit.conditionguns,unit.inmelee,unit.timedmovement,unit.totaltimeoforderdelay}});
                Pose(records,"pose",key+"/root",key,unit.transform,true);
                records.Add(new MpTacticalFingerprintRecord{Kind="unit",Key=key+"/ammo",Parent=key,Values=unit.ammo==null?null:(float[])unit.ammo.Clone()});
                records.Add(new MpTacticalFingerprintRecord{Kind="unit",Key=key+"/losses",Parent=key,Integers=unit.losses==null?null:(int[])unit.losses.Clone()});
                records.Add(new MpTacticalFingerprintRecord{Kind="unit",Key=key+"/orders",Parent=key,Flags=unit.ordertypeactive==null?null:(bool[])unit.ordertypeactive.Clone()});
                records.Add(new MpTacticalFingerprintRecord{Kind="unit",Key=key+"/block-paths",Parent=key,Integers=unit.blockobjectcurrentpath==null?null:(int[])unit.blockobjectcurrentpath.Clone()});
                records.Add(new MpTacticalFingerprintRecord{Kind="unit",Key=key+"/block-corners",Parent=key,Integers=unit.blockobjectcurrentcorner==null?null:(int[])unit.blockobjectcurrentcorner.Clone()});
                // Block indices are native formation-section identities. They are
                // regenerated on load and must agree before this proof is released.
                if(unit.blockobject!=null)for(int i=0;i<unit.blockobject.Length;i++)
                    Pose(records,"block",key+"/block/"+i.ToString(System.Globalization.CultureInfo.InvariantCulture),key,unit.blockobject[i]==null?null:unit.blockobject[i].transform,unit.blockobject[i]!=null && unit.blockobject[i].activeSelf);
            }
            if(GameVars.commander==null)throw new InvalidDataException("Initialized native officers are missing.");
            for(int i=0;i<GameVars.commander.Count;i++) {
                var c=GameVars.commander[i];if(c==null)throw new InvalidDataException("Initialized native officer table contains a gap.");
                records.Add(new MpTacticalFingerprintRecord{Kind="officer",Key=i.ToString(System.Globalization.CultureInfo.InvariantCulture),Parent="",Text=new[]{c.name??"",c.firstname??"",Reference(c.currentcommand)},
                    Integers=new[]{c.alliance,c.rank,c.status},Values=new[]{c.exp,c.fame,c.initiative,c.leadership,c.administration,c.cunning}});
            }
            if(!campaign) {
                if(BattleUnits.objectives==null || controller.sideinformation==null || controller.sideinformation.Length!=2)throw new InvalidDataException("Initialized tactical objectives or sides are missing.");
                foreach(var objective in BattleUnits.objectives) {
                    if(objective==null)throw new InvalidDataException("Initialized tactical objective is missing.");
                    string key=objective.name;
                    records.Add(new MpTacticalFingerprintRecord{Kind="objective",Key=key,Parent="",Text=new[]{objective.objectivename??"",Reference(NativeAccess.Get<Regiment>(objective,"lastregimenttotake"))},
                        Integers=new[]{objective.objectivetype,objective.objectivetimer,objective.owner,objective.points,NativeAccess.Get<int>(objective,"alliancetotake"),NativeAccess.Get<int>(objective,"currentoccupator"),NativeAccess.Get<int>(objective,"lastoccupator"),NativeAccess.Get<int>(objective,"objectivestrength")},
                        Flags=new[]{objective.gameObject.activeSelf,objective.gameObject.activeInHierarchy},
                        Values=new[]{objective.objectivetimevalue,NativeAccess.Get<float>(objective,"initialtimevalue"),NativeAccess.Get<float>(objective,"occupationratio")}.Concat(objective.alliancepoints??new float[0]).ToArray()});
                    Pose(records,"pose",key+"/objective",key,objective.transform,true);
                }
                for(int i=0;i<2;i++) {
                    var s=controller.sideinformation[i];if(s==null)throw new InvalidDataException("Initialized tactical side is missing.");
                    records.Add(new MpTacticalFingerprintRecord{Kind="side",Key=i.ToString(System.Globalization.CultureInfo.InvariantCulture),Parent="",
                        Integers=new[]{s.totalstrengthinf,s.totalstrengthcav,s.totalstrengthari,s.guns,s.gunslost,s.totalwounded,s.totalkilled,s.totalmissing,s.totalcaptured,s.totalsick,s.strengthtoarrive,s.corpstoarrive,s.leftfield},
                        Values=new[]{s.fightingspirit,s.ammunition,s.conditionmen,s.conditionhorses,s.conditionguns,s.averagemorale,s.objectivepoints,s.totalpoints,s.totallosses,s.pointsfromobjectives}});
                }
            }
            return records.ToArray();
        }
        static Regiment Parent(Regiment unit)
        {
            for(var node=unit.transform.parent;node!=null && node!=BattleUnits.unitfolder.transform;node=node.parent) {
                var parent=node.GetComponent<Regiment>();if(parent!=null)return parent;
            }
            return null;
        }
        static void Pose(List<MpTacticalFingerprintRecord> records,string kind,string key,string parent,Transform transform,bool active)
        {
            if(transform==null){records.Add(new MpTacticalFingerprintRecord{Kind=kind,Key=key,Parent=parent,Flags=new[]{false}});return;}
            var p=transform.position;var r=transform.eulerAngles;
            records.Add(new MpTacticalFingerprintRecord{Kind=kind,Key=key,Parent=parent,Flags=new[]{true,active},
                Integers=new[]{MpTacticalFingerprint.Angle(r.x),MpTacticalFingerprint.Angle(r.y),MpTacticalFingerprint.Angle(r.z)},Values=new[]{p.x,p.y,p.z}});
        }
    }
}
