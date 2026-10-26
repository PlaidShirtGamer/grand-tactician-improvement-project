using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GTIP
{
    // Pure preflight for the supported, non-DLC saved-campaign-battle path.
    // This does not execute native parsing or establish live battlefield parity.
    public sealed class MpTacticalLoadPlan
    {
        public readonly string InstallRoot, SaveRoot, Scenario, Folder, Battlefield, ContentHash;
        public readonly int Side, President;
        public readonly bool Returning;
        public readonly float Version;
        public string Level => Scenario.Substring(0,3);
        public string Sublevel => Scenario.Substring(4,1);
        public string BattleSublevel => "A";
        const string DataDirectory="The Civil War (1861-1865)_Data";
        static readonly string[] RootRequired=("addcommanders.dat AICampaign.dat AlarmInfos.dat AllObjects.dat armygrouprefs.dat attributes.dat battledata.dat buildings.dat CampaignObjectives.dat ChartDatabase.dat commanders.txt garrisonrefs.dat groups.dat IIPsTowns.dat nations.dat paths.dat Railroads.dat regiments.dat scenario.dat ships.dat shiptype.dat spottings.dat version.dat weapons.dat").Split(' ');
        static readonly string[] BattleRequired=("armygrouprefs.dat artillery.dat attributes.dat battledata.dat battleunits.dat buildings.dat commanders.txt entrypoints.dat fallenlayers.dat frontlines.dat groups.dat objectives.dat obstacles.dat paths.dat pontoons.dat regiments.dat scenario.dat sideinfo.dat version.dat").Split(' ');

        MpTacticalLoadPlan(string install,string save,string scenario,string folder,string battlefield,int side,int president,bool returning,float version,string contentHash)
        {InstallRoot=install;SaveRoot=save;Scenario=scenario;Folder=folder;Battlefield=battlefield;Side=side;President=president;Returning=returning;Version=version;ContentHash=contentHash;}

        public static void ValidateScenario(string scenario)
        {
            if(scenario==null || !Regex.IsMatch(scenario,@"\A[0-9]{3}/[A-Za-z0-9]\z"))throw new InvalidDataException("Invalid agreed campaign scenario.");
        }
        public static void CheckPath(string path)
        {
            for(string p=Path.GetFullPath(path);p!=null;p=Path.GetDirectoryName(p))
                if((File.Exists(p)||Directory.Exists(p)) && (File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)
                    throw new IOException("Native tactical path contains a symbolic link or junction: "+p);
        }
        static string Normalize(string path)=>Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        public static MpTacticalLoadPlan Read(string installRoot,string extractedRoot,string expectedCampaign,int side,bool returning,float expectedVersion)
        {
            ValidateScenario(expectedCampaign);
            if(side<0 || side>1 || !Finite(expectedVersion) || expectedVersion<=0)throw new InvalidDataException("Unsupported tactical faction or native version.");
            var install=Normalize(installRoot);var save=Normalize(extractedRoot);
            var baseCampaign=Path.Combine(install,"Campaigns",expectedCampaign.Replace('/',Path.DirectorySeparatorChar));
            CheckPath(install);CheckPath(save);
            if(!Directory.Exists(baseCampaign) || !Directory.Exists(save) || !string.Equals(Path.GetDirectoryName(save),Normalize(baseCampaign),StringComparison.OrdinalIgnoreCase))
                throw new IOException("Native tactical save must be a direct child of the agreed installed campaign.");
            var folder=Path.GetFileName(save);
            if(!Regex.IsMatch(folder,@"\A[A-Za-z0-9_-]{1,100}\z"))throw new InvalidDataException("Invalid isolated tactical save folder.");
            Required(save,RootRequired);
            float version=ReadVersion(Path.Combine(save,"version.dat"),expectedVersion);
            var root=ScenarioData(Path.Combine(save,"scenario.dat"),expectedCampaign,side,false);
            string battlefield="";
            if(!returning) {
                var battle=Path.Combine(save,"Battle");Required(battle,BattleRequired);ReadVersion(Path.Combine(battle,"version.dat"),expectedVersion);
                var tactical=ScenarioData(Path.Combine(battle,"scenario.dat"),expectedCampaign,side,true);battlefield=tactical[13];
                if(!Directory.Exists(Path.Combine(install,"Battlefields",battlefield,"A")))throw new IOException("Required battlefield definition is not installed: "+battlefield);
                CountHeader(Path.Combine(battle,"regiments.dat"),20000);CountHeader(Path.Combine(battle,"groups.dat"),20000);CountHeader(Path.Combine(battle,"commanders.txt"),100000);
            }
            CountHeader(Path.Combine(save,"regiments.dat"),20000);CountHeader(Path.Combine(save,"groups.dat"),20000);CountHeader(Path.Combine(save,"commanders.txt"),100000);CountHeader(Path.Combine(save,"nations.dat"),10000);
            string hash=ContentDigest(install,baseCampaign,battlefield);
            return new MpTacticalLoadPlan(install,save,expectedCampaign,folder,battlefield,side,Int(root[11]),returning,version,hash);
        }
        static void Required(string root,IEnumerable<string> files)
        {
            foreach(var name in files) {
                var path=Path.Combine(root,name);CheckPath(path);
                if(!File.Exists(path))throw new InvalidDataException("Native tactical load requires "+name+" in "+Path.GetFileName(root)+".");
            }
        }
        static string[] Lines(string path,int maxBytes)
        {
            CheckPath(path);var info=new FileInfo(path);
            if(!info.Exists || info.Length>maxBytes)throw new InvalidDataException("Missing or oversized native metadata: "+Path.GetFileName(path));
            return File.ReadAllLines(path);
        }
        static float ReadVersion(string path,float expected)
        {
            var lines=Lines(path,256);
            if(lines.Length!=1 || !float.TryParse(lines[0],NumberStyles.Float,CultureInfo.InvariantCulture,out float value) || !Finite(value) || value!=expected)
                throw new InvalidDataException("Native tactical save version differs from the installed supported build.");
            return value;
        }
        static void CountHeader(string path,int limit)
        {
            using(var reader=new StreamReader(path)) {
                string line=reader.ReadLine();
                if(line==null || line.Length>20 || !int.TryParse(line,NumberStyles.Integer,CultureInfo.InvariantCulture,out int count) || count<0 || count>limit)
                    throw new InvalidDataException("Invalid native record count: "+Path.GetFileName(path));
                if(count>0 && reader.ReadLine()==null)throw new InvalidDataException("Truncated native table: "+Path.GetFileName(path));
            }
        }
        static string[] ScenarioData(string path,string campaign,int side,bool battle)
        {
            var lines=Lines(path,256*1024);
            if(lines.Length!=31 || lines.Any(l=>l.Length>8192 || l.IndexOf('\0')>=0))throw new InvalidDataException("Unsupported native scenario metadata shape.");
            if(Int(lines[0])!=side || lines[12]!=campaign)throw new InvalidDataException("Native checkpoint campaign or faction differs from the session.");
            Date(lines,2,3,4);Date(lines,5,6,7);
            Range(Int(lines[8]),0,23);Range(Int(lines[9]),0,59);Bool(lines[10]);Range(Int(lines[11]),-2,10000);
            if(battle) {
                if(!Regex.IsMatch(lines[13],@"\A[0-9]{3}\z") || lines[13]=="999")throw new InvalidDataException("Invalid saved battlefield scene identifier.");
            } else if(lines[13]!="" && !Regex.IsMatch(lines[13],@"\A[0-9]{3}\z"))throw new InvalidDataException("Invalid campaign battlefield metadata.");
            Bool(lines[14]);Number(lines[15],0,100000);Number(lines[17],0,100);
            if(Int(lines[18])!=0 || Int(lines[19])!=0 || Int(lines[20])!=0)Date(lines,18,19,20);
            if(Bool(lines[21]))throw new InvalidDataException("Tutorial saves are outside the tactical co-op proof.");
            Range(Int(lines[22]),0,10000);Number(lines[23],float.Epsilon,100);
            for(int i=26;i<=29;i++)Bool(lines[i]);
            if(Int(lines[30])!=-1)throw new InvalidDataException("Tactical co-op requires the regimental campaign hierarchy.");
            return lines;
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        static int Int(string text)
        {if(!int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out int value))throw new InvalidDataException("Invalid integer in native scenario metadata.");return value;}
        static void Range(int v,int min,int max){if(v<min||v>max)throw new InvalidDataException("Native scenario value exceeds supported bounds.");}
        static bool Bool(string text){if(!bool.TryParse(text,out bool value))throw new InvalidDataException("Invalid boolean in native scenario metadata.");return value;}
        static void Number(string text,float min,float max)
        {if(!float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float v)||!Finite(v)||v<min||v>max)throw new InvalidDataException("Invalid numeric native scenario metadata.");}
        static void Date(string[] lines,int day,int month,int year)
        {try{new DateTime(Int(lines[year]),Int(lines[month]),Int(lines[day]));}catch(ArgumentOutOfRangeException){throw new InvalidDataException("Invalid native scenario date.");}}

        static string ContentDigest(string install,string campaign,string battlefield)
        {
            var files=new SortedDictionary<string,string>(StringComparer.Ordinal);
            void Add(string label,string path){CheckPath(path);if(!File.Exists(path)||new FileInfo(path).Length==0)throw new IOException("Required installed tactical content is missing: "+label);files.Add(label,path);}
            string Config(string name)=>Path.Combine(install,"Config",name);
            string Effective(string name)=>File.Exists(Path.Combine(campaign,name))?Path.Combine(campaign,name):Config(name);
            Add("effective/unitprefs.txt",Effective("unitprefs.txt"));Add("effective/campaignprefs.txt",Effective("campaignprefs.txt"));Add("config/battleprefs.txt",Config("battleprefs.txt"));
            foreach(var name in new[]{"weapons.txt","weaponclasses.dat","weapongroups.dat","shiptypes.dat","buildingtypes.dat","weather.txt"})Add("config/"+name,Config(name));
            Add("campaign/nations.dat",Path.Combine(campaign,"nations.dat"));Add("effective/ranks.txt",Effective("ranks.txt"));
            // Per-alliance rank overrides are optional native inputs. Absence is
            // part of the fingerprint rather than silently sharing the other peer's ranks.
            string ranksRoot=File.Exists(Path.Combine(campaign,"ranks.txt"))?campaign:Path.Combine(install,"Config");
            foreach(string path in Directory.GetFiles(ranksRoot,"ranks*.txt"))
                if(Regex.IsMatch(Path.GetFileName(path),@"\Aranks[0-9]+\.txt\z",RegexOptions.IgnoreCase))Add("optional/"+Path.GetFileName(path).ToLowerInvariant(),path);
            string dlcConfig=Effective("dlcwl_config.dat");if(File.Exists(dlcConfig))Add("effective/dlcwl_config.dat",dlcConfig);
            var data=Path.Combine(install,DataDirectory);Add("unity/globalgamemanagers",Path.Combine(data,"globalgamemanagers"));
            string stream=Path.Combine(data,"StreamingAssets");Add("unity/campaignmap.manifest",Path.Combine(stream,"campaignmap.manifest"));
            if(battlefield!="") {
                Add("unity/battlemap.manifest",Path.Combine(stream,"battlemap.manifest"));
                foreach(var name in new[]{"battledata.dat","nations.dat"})Add("battlefield/"+name,Path.Combine(install,"Battlefields",battlefield,"A",name));
            }
            using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes,Encoding.UTF8)) {
                writer.Write("GTIP tactical content descriptors v1");writer.Write(battlefield);
                foreach(var pair in files)using(var input=new FileStream(pair.Value,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create()) {
                    writer.Write(pair.Key);writer.Write(input.Length);writer.Write(sha.ComputeHash(input));
                }
                foreach(var name in battlefield==""?new[]{"campaignmap"}:new[]{"campaignmap","battlemap"}) {
                    string path=Path.Combine(stream,name);CheckPath(path);if(!File.Exists(path)||new FileInfo(path).Length==0)throw new IOException("Required installed terrain bundle is missing: "+name);
                    writer.Write("bundle-length/"+name);writer.Write(new FileInfo(path).Length);
                }
                writer.Flush();using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(bytes.ToArray()));
            }
        }
    }
}
