#nullable enable
using System.Reflection;

namespace UnityEngine
{
    public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
    public class Transform
    {
        public readonly GameObject gameObject;public Transform? parent;public List<Transform> children=new();public Vector3 position,eulerAngles;
        public Transform(GameObject gameObject){this.gameObject=gameObject;}
        public void Parent(Transform? target){parent?.children.Remove(this);parent=target;target?.children.Add(this);}
        public T? GetComponent<T>()where T:class=>gameObject.GetComponent<T>();
    }
    public abstract class Component {public GameObject gameObject=null!;public Transform transform=>gameObject.transform;public string name=>gameObject.name;}
    public class GameObject
    {
        static readonly Dictionary<string,GameObject> registry=new();readonly List<Component> components=new();
        public string name;public readonly Transform transform;public bool activeSelf=true;public bool activeInHierarchy=>activeSelf && (transform.parent==null || transform.parent.gameObject.activeInHierarchy);
        public GameObject(string name){this.name=name;transform=new(this);registry[name]=this;}
        public T Add<T>(T component)where T:Component{component.gameObject=this;components.Add(component);return component;}
        public T? GetComponent<T>()where T:class=>components.OfType<T>().FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool inactive)where T:class {var list=new List<T>();void Visit(Transform node){if(inactive || node.gameObject.activeInHierarchy)list.AddRange(node.gameObject.components.OfType<T>());foreach(var child in node.children)Visit(child);}Visit(transform);return list.ToArray();}
        public static GameObject? Find(string name)=>registry.TryGetValue(name,out var value)?value:null;
    }
}
public class Regiment:UnityEngine.Component
{
    public bool initialized=true;public int alliance,unittyp,commander,nation,strength=1000,sick,guns,weapon,formation,groupformation,formationordered,mounted,surrenderstate,combatbehavior,combatbehaviorordered,unitstatus,blockobjects=1,blockobjectsused=1,regimentpaths,movementmode;
    public string abrvname="";public bool detached,permanentlydetached,isrouted,running,doublequick,pathinterrupted;public float timeofarrival=-1,morale=.8f,fatigue=.1f,training=.4f,experience=.2f,cohesion=.8f,conditionmen=1,conditionhorses=1,conditionguns=1,inmelee,timedmovement,totaltimeoforderdelay;
    public bool[]? ordertypeactive=new[]{false,true};public int[]? blockobjectcurrentpath=new[]{0},blockobjectcurrentcorner=new[]{0};
    public UnityEngine.GameObject? parentregiment;public UnityEngine.GameObject[]? blockobject;public float[]? ammo=new[]{.8f,.9f};public int[]? losses=new[]{1,2,3};
}
public class UniStormWeatherSystem_C {public int monthCounter=3,dayCounter=1;public float Hour=8;}
public class BattleUnits:UnityEngine.Component
{
    public bool IsCampaign;public int year=1863,battlepasseddays;public float battletime=8;public UniStormWeatherSystem_C? uniStormSystem=new();
    public static UnityEngine.GameObject? unitfolder;public static Objectives[]? objectives;public SideInformation[]? sideinformation=new[]{new SideInformation(),new SideInformation()};
    public class SideInformation {public int totalstrengthinf=1000,totalstrengthcav,totalstrengthari,guns,gunslost,totalwounded,totalkilled,totalmissing,totalcaptured,totalsick,strengthtoarrive,corpstoarrive,leftfield;public float fightingspirit=1,ammunition=.8f,conditionmen=1,conditionhorses=1,conditionguns=1,averagemorale=.8f,objectivepoints=50,totalpoints=50,totallosses,pointsfromobjectives=50;}
}
public class Objectives:UnityEngine.Component
{
    public string objectivename="Crossroads";public int objectivetype=1,objectivetimer,owner,points=50;public float objectivetimevalue=2;public float[]? alliancepoints=new[]{50f,0f};
    public int alliancetotake=-1,currentoccupator,lastoccupator,objectivestrength=100;public float initialtimevalue=2,occupationratio=.5f;public Regiment? lastregimenttotake;
}
public static class GameVars
{
    public static bool gamepaused=true,allregimentsinitialized=true;public static int frame=60,playeralliance;public static float currenttimefromstart=8;
    public static List<Commander>? commander;
    public class Commander {public string name="Runyon",firstname="Theodore";public Regiment? currentcommand;public int alliance,rank=2,status;public float exp=.2f,fame=.3f,initiative=.4f,leadership=.5f,administration=.6f,cunning=.7f;}
}
public static class SceneManagement {public static string currentcampaignscenario="001/A";}
public static class GamePrefs {public static string leveltoload="212",subleveltoload="A";}
namespace GTIP
{
    internal static class NativeAccess {public static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(target)!;}
}
