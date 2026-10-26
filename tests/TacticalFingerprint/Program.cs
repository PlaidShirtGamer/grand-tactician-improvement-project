using GTIP;
using System.Globalization;

int checks=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
void Reject(Action action,string message){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,message);}
MpTacticalFingerprintRecord[] Fixture()=>new[]{
 new MpTacticalFingerprintRecord{Kind="world",Key="context",Text=new[]{"001/A","212/A"},Integers=new[]{1863,3,1},Values=new[]{8f,8f}},
 new MpTacticalFingerprintRecord{Kind="unit",Key="army",Parent="",Text=new[]{"Army"},Integers=new[]{0,18,1,32000},Flags=new[]{true},Values=new[]{.8f,.9f}},
 new MpTacticalFingerprintRecord{Kind="unit",Key="regiment",Parent="army",Text=new[]{"5th New Jersey"},Integers=new[]{0,0,20,1000,0,12},Flags=new[]{true,false},Values=new[]{.75f,.02f}},
 new MpTacticalFingerprintRecord{Kind="pose",Key="regiment/root",Parent="regiment",Integers=new[]{0,MpTacticalFingerprint.Angle(90),0},Values=new[]{100f,1f,200f}},
 new MpTacticalFingerprintRecord{Kind="block",Key="regiment/block/0",Parent="regiment",Integers=new[]{0,MpTacticalFingerprint.Angle(90),0},Values=new[]{101f,1f,201f}},
 new MpTacticalFingerprintRecord{Kind="unit",Key="regiment/ammo",Parent="regiment",Values=new[]{.45f,.9f}},
 new MpTacticalFingerprintRecord{Kind="objective",Key="Crossroads",Integers=new[]{1,0,5},Values=new[]{20f,1f}},
 new MpTacticalFingerprintRecord{Kind="officer",Key="20",Text=new[]{"Runyon","Theodore","regiment"},Integers=new[]{0,2,0},Values=new[]{.3f,.7f}},
 new MpTacticalFingerprintRecord{Kind="side",Key="0",Integers=new[]{32000,2000,50},Values=new[]{.8f,200f}}
};
var original=Fixture();string hash=MpTacticalFingerprint.Hash(original);
Check(Convert.FromBase64String(hash).Length==32,"Baseline uses the protocol's canonical SHA-256 digest");
Check(hash==MpTacticalFingerprint.Hash(original.Reverse()),"Native enumeration order does not change initialized-world identity");
Check(MpTacticalFingerprint.FirstDifference(original,original.Reverse())==null,"Diagnostics use the same canonical ordering as hashing");
Check(MpTacticalFingerprint.Segment(0,1,2,"3:unit")!=MpTacticalFingerprint.Segment(0,1,23,":unit"),"Length-prefixed unit identity cannot collide at delimiters");
var culture=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");Check(hash==MpTacticalFingerprint.Hash(original),"Fingerprint encoding is independent of local number formatting");}finally{CultureInfo.CurrentCulture=culture;}
void Changed(Action<MpTacticalFingerprintRecord[]> mutate,string message){var altered=Fixture();mutate(altered);Check(MpTacticalFingerprint.Hash(altered)!=hash,message);Check(MpTacticalFingerprint.FirstDifference(original,altered)!=null,"Diagnostics identify "+message);}
Changed(x=>x[2].Integers[3]--,"One missing soldier changes the baseline");
Changed(x=>x[2].Integers[5]++,"Equipment differences change the baseline");
Changed(x=>x[2].Values[0]-=.001f,"Morale differences change the baseline");
Changed(x=>x[5].Values[0]-=.001f,"Ammunition differences change the baseline");
Changed(x=>x[2].Flags[0]=false,"Inactive and active units are distinct");
Changed(x=>x[2].Parent="","Hierarchy changes alter the baseline");
Changed(x=>x[3].Values[0]+=.01f,"Root position differences change the baseline");
Changed(x=>x[4].Values[0]+=.01f,"Formation-section geometry differences change the baseline");
Changed(x=>x[4].Integers[1]++,"Formation facing differs at millidegree precision");
Changed(x=>x[6].Integers[1]++,"Objective ownership differences change the baseline");
Changed(x=>x[7].Integers[1]++,"Officer rank differences change the baseline");
Changed(x=>x[8].Values[1]++,"Battle side point totals change the baseline");
Changed(x=>x[0].Values[0]+=.001f,"Battle clock differences change the baseline");
var geometry=Fixture();geometry[3].Values[0]+=.00001f;Check(MpTacticalFingerprint.Hash(geometry)==hash,"Sub-millimetre transform noise stays in the same fixed quantization bin");
var signed=Fixture();signed[3].Values[1]=0;signed[3].Values[2]=-0f;var positive=Fixture();positive[3].Values[1]=0;positive[3].Values[2]=0f;Check(MpTacticalFingerprint.Hash(signed)==MpTacticalFingerprint.Hash(positive),"Signed zero cannot create a false geometry mismatch");
Check(MpTacticalFingerprint.Angle(0)==MpTacticalFingerprint.Angle(360) && MpTacticalFingerprint.Angle(-90)==MpTacticalFingerprint.Angle(270),"Equivalent full-turn angle representations are canonical");
Check(MpTacticalFingerprint.Angle(359.99999f)==MpTacticalFingerprint.Angle(0),"Angle quantization wraps a rounded full turn to zero");
Reject(()=>MpTacticalFingerprint.Hash(null!),"Missing baseline is rejected");
Reject(()=>MpTacticalFingerprint.Hash(new[]{original[2],original[2]}),"Duplicate native identities cannot be paired arbitrarily");
var missing=Fixture();missing[2].Parent="unknown";Reject(()=>MpTacticalFingerprint.Hash(missing),"References to missing parents are rejected");
var cycle=Fixture();cycle[1].Parent="regiment";Reject(()=>MpTacticalFingerprint.Hash(cycle),"Native hierarchy cycles cannot enter readiness");
var nan=Fixture();nan[2].Values[0]=float.NaN;Reject(()=>MpTacticalFingerprint.Hash(nan),"NaN gameplay state cannot satisfy readiness");
var infinity=Fixture();infinity[3].Values[0]=float.PositiveInfinity;Reject(()=>MpTacticalFingerprint.Hash(infinity),"Infinite geometry cannot satisfy readiness");
var huge=Fixture();huge[2].Values[0]=float.MaxValue;Reject(()=>MpTacticalFingerprint.Hash(huge),"Extreme gameplay values cannot overflow canonical integer bins");
var text=Fixture();text[2].Text=new[]{new string('x',16385)};Reject(()=>MpTacticalFingerprint.Hash(text),"Unbounded native text is rejected");
var array=Fixture();array[2].Values=new float[4097];Reject(()=>MpTacticalFingerprint.Hash(array),"Unbounded native arrays are rejected");
var unknown=Fixture();unknown[2].Kind="executable";Reject(()=>MpTacticalFingerprint.Hash(unknown),"Unknown record schemas are rejected");
var exact=Fixture();exact[2].Text=null;var empty=Fixture();empty[2].Text=Array.Empty<string>();Check(MpTacticalFingerprint.Hash(exact)!=MpTacticalFingerprint.Hash(empty),"Missing state is distinguishable from an initialized empty array");
Console.WriteLine($"Tactical fingerprint: {checks} checks. Native Unity world capture is not run by this suite.");
NativeStateChecks.Run();
