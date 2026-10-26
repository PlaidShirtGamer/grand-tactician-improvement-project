#nullable enable
using GTIP;
using System.Text;

int checks=0;
void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
void Reject(Action action,string message){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,message);}
MpMessage Wire(MpMessage value)=>MpCodec.Decode(MpCodec.Encode(value));
string Digest(string text)=>MpCheckpoint.Hash(Encoding.UTF8.GetBytes(text));
const string session="connected-campaign";const int generation=7,revision=41;
string hash=Digest("last campaign state accepted by both peers"),otherHash=Digest("unapplied campaign state");
MpTacticalDrain NewDrain()=>new(session,generation);
MpMessage Held()=>Wire(MpTacticalDrain.Receipt(session,generation,revision,hash));

Reject(()=>new MpTacticalDrain(null!,generation),"A missing connection identity cannot create a handoff");
Reject(()=>new MpTacticalDrain("",generation),"An empty connection identity cannot create a handoff");
Reject(()=>new MpTacticalDrain(new string('s',129),generation),"Unbounded connection identity cannot create a handoff");
Reject(()=>new MpTacticalDrain(session,0),"The pre-battle generation cannot create a handoff");
Reject(()=>new MpTacticalDrain(session,-1),"Negative generations cannot create a handoff");

var drain=NewDrain();var offer=Wire(drain.Offer());
MpTacticalDrain.ValidateOffer(offer,session,generation);
Check(offer.kind=="tactical-hold" && offer.session==session && offer.sequence==generation,"A hold offer survives the production wire format with its connection and transition context");
Check(!drain.PeerHeld && !drain.Committed,"Creating and sending a hold does not acknowledge the guest");
Check(!drain.Commit(false,revision,hash),"The host cannot destroy campaign objects before the guest's hold receipt arrives");
Check(!drain.Commit(true,revision,hash),"A busy host cannot commit while waiting for the guest");
Check(drain.Receive(Held()),"The first exact campaign revision receipt acknowledges the guest");
Check(drain.PeerHeld && !drain.Committed,"A guest receipt preserves the separate host completion barrier");
Check(!drain.Commit(true,revision,hash) && !drain.Committed,"An in-flight campaign capture keeps the handoff closed after the guest is held");
Check(!drain.Receive(Held()),"A reliable transport retry of the same receipt is harmless");
Check(drain.Commit(false,revision,hash) && drain.Committed,"The host commits once after draining work and matching the applied campaign revision");
Check(!drain.Commit(false,revision,hash),"A completed handoff cannot schedule another native scene load");
Check(!drain.Receive(Held()) && drain.Committed,"A delayed identical receipt cannot reopen a completed handoff");

var fence=Wire(MpTacticalDrain.Fence(session,generation,revision,hash));
MpTacticalDrain.ValidateFence(fence,session,generation,revision,hash);
Check(fence.kind=="tactical-drained" && fence.baseRevision==revision && fence.stateHash==hash,"The final FIFO fence carries the exact host-applied revision through real wire encoding");
MpTacticalDrain.ValidateFence(fence,session,generation,revision,hash);
Check(true,"An identical drain fence may be validated again without changing state");
Reject(()=>MpTacticalDrain.ValidateFence(fence,session,generation,revision-1,hash),"Guest cannot acknowledge a fence before applying its last campaign revision");
Reject(()=>MpTacticalDrain.ValidateFence(fence,session,generation,revision+1,hash),"A stale final fence cannot acknowledge a newer local campaign revision");
Reject(()=>MpTacticalDrain.ValidateFence(fence,session,generation,revision,otherHash),"Fence revision equality cannot hide a different applied state");
Reject(()=>MpTacticalDrain.ValidateFence(fence,session,generation,0,hash),"An uninitialized guest baseline cannot acknowledge the final fence");
Reject(()=>MpTacticalDrain.ValidateFence(fence,session,generation,revision,"broken"),"Malformed local state cannot acknowledge the final fence");
Reject(()=>MpTacticalDrain.ValidateFence(Held(),session,generation,revision,hash),"The guest's receipt cannot be confused with the host's final fence");
Reject(()=>MpTacticalDrain.ValidateFence(null!,session,generation,revision,hash),"A missing final fence is rejected");
Reject(()=>MpTacticalDrain.Fence(session,generation,0,hash),"A final fence requires an applied campaign revision");
Reject(()=>MpTacticalDrain.Fence(session,generation,revision,"broken"),"A final fence requires a canonical campaign digest");
var zeroFence=Wire(fence);zeroFence.baseRevision=0;
Reject(()=>MpTacticalDrain.ValidateFence(zeroFence,session,generation,0,hash),"Matching zero revisions cannot satisfy the final fence");
var badFence=Wire(fence);badFence.stateHash="broken";
Reject(()=>MpTacticalDrain.ValidateFence(badFence,session,generation,revision,"broken"),"Matching malformed digest strings cannot satisfy the final fence");

var newer=MpTacticalDrain.Receipt(session,generation,revision+1,hash);
Reject(()=>drain.Receive(newer),"A conflicting revision cannot replace the accepted receipt even after commit");
var changed=MpTacticalDrain.Receipt(session,generation,revision,otherHash);
Reject(()=>drain.Receive(changed),"A conflicting digest cannot replace the accepted receipt");
Check(drain.PeerHeld && drain.Committed,"Conflicting retries leave the existing committed state intact");

var delayed=NewDrain();delayed.Receive(Held());
Reject(()=>delayed.Commit(false,revision+1,hash),"A delayed guest receipt cannot commit a newer unacknowledged host revision");
Reject(()=>delayed.Commit(false,revision,otherHash),"Equal revision numbers with different applied state cannot start scene loading");
Check(!delayed.Committed,"Revision or digest disagreement keeps the host uncommitted");
Check(delayed.Commit(false,revision,hash),"A rejected mismatch does not corrupt the accepted guest receipt");

var failed=NewDrain();var malformed=Held();malformed.stateHash="broken";
Reject(()=>failed.Receive(malformed),"Malformed digests cannot create a held state");
Check(!failed.PeerHeld && !failed.Committed,"Malformed receipt rejection preserves the unacknowledged state");
Check(failed.Receive(Held()),"A valid receipt remains usable after a rejected malformed message");
Reject(()=>failed.Commit(false,revision,"broken"),"An invalid local digest cannot satisfy the host's barrier");
Check(!failed.Committed,"Invalid local state cannot mark the handoff committed");

Reject(()=>MpTacticalDrain.Receipt(session,generation,0,hash),"A receipt must refer to an applied campaign revision");
Reject(()=>MpTacticalDrain.Receipt(session,generation,-1,hash),"Negative campaign revisions cannot be acknowledged");
foreach(var invalid in new string?[]{null,"",hash+" "," "+hash,"not-a-digest",Convert.ToBase64String(new byte[31]),Convert.ToBase64String(new byte[33])})
 Reject(()=>MpTacticalDrain.Receipt(session,generation,revision,invalid!),"Receipt rejects a missing, malformed, wrong-length, or whitespace-padded SHA-256 value");
string alternate=Convert.ToBase64String(new byte[32]);alternate=alternate.Substring(0,42)+"B=";
Check(Convert.FromBase64String(alternate).SequenceEqual(new byte[32]),"The noncanonical test digest decodes to the same 32 bytes");
Reject(()=>MpTacticalDrain.Receipt(session,generation,revision,alternate),"Equivalent noncanonical Base64 cannot produce competing textual digest identities");

// Control frames cannot carry gameplay payloads or be reused across a connection,
// transition, protocol version, or restored campaign world.
var pollution=new (string Name,Action<MpMessage> Apply)[]{
 ("message kind",m=>m.kind="snapshot"),("protocol version",m=>m.protocol=MpRules.Protocol-1),
 ("connection identity",m=>m.session="another-connection"),("previous transition",m=>m.sequence=generation-1),
 ("future transition",m=>m.sequence=generation+1),("zero transition",m=>m.sequence=0),
 ("restored-world generation",m=>m.worldGeneration=1),("token",m=>m.token="x"),
 ("mode",m=>m.mode="coop"),("text",m=>m.text="x"),("campaign",m=>m.campaign="x"),
 ("unit",m=>m.unit=1),("clock",m=>m.clock=new MpClock()),("checkpoint bytes",m=>m.blob=new byte[]{1}),
 ("unit snapshot",m=>m.units=new[]{new MpUnit()}),("town snapshot",m=>m.towns=new[]{new MpTown()}),
 ("side",m=>m.side=1),("x coordinate",m=>m.x=1),("z coordinate",m=>m.z=1),
 ("date",m=>m.date=1),("pause flag",m=>m.paused=true),("nonfinite coordinate",m=>m.x=float.NaN)
};
foreach(var item in pollution) {
 var dirtyOffer=NewDrain().Offer();item.Apply(dirtyOffer);
 Reject(()=>MpTacticalDrain.ValidateOffer(dirtyOffer,session,generation),"Hold offer rejects unrelated "+item.Name);
 var dirtyReceipt=Held();item.Apply(dirtyReceipt);var isolated=NewDrain();
 Reject(()=>isolated.Receive(dirtyReceipt),"Hold receipt rejects unrelated "+item.Name);
 Check(!isolated.PeerHeld && !isolated.Committed,"Rejected "+item.Name+" cannot partially satisfy the handoff");
 var dirtyFence=Wire(fence);item.Apply(dirtyFence);
 Reject(()=>MpTacticalDrain.ValidateFence(dirtyFence,session,generation,revision,hash),"Final drain fence rejects unrelated "+item.Name);
}
Reject(()=>MpTacticalDrain.ValidateOffer(null!,session,generation),"Missing hold offer is rejected");
Reject(()=>NewDrain().Receive(null!),"Missing hold receipt is rejected");
var revisionInOffer=NewDrain().Offer();revisionInOffer.baseRevision=revision;
Reject(()=>MpTacticalDrain.ValidateOffer(revisionInOffer,session,generation),"The host offer cannot pretend to acknowledge the guest's applied revision");
var digestInOffer=NewDrain().Offer();digestInOffer.stateHash=hash;
Reject(()=>MpTacticalDrain.ValidateOffer(digestInOffer,session,generation),"The host offer cannot carry a guest receipt digest");
var zeroReceipt=Held();zeroReceipt.baseRevision=0;
Reject(()=>NewDrain().Receive(zeroReceipt),"A crafted receipt cannot bypass the positive applied-revision rule");

// A restored campaign starts a new namespace for ordinary state frames. This
// field must survive the same codec as transition controls, separate from both
// command sequence and campaign snapshot revision.
foreach(int world in new[]{0,1,int.MaxValue}) {
 var state=new MpMessage{kind="snapshot",session=session,sequence=123,baseRevision=revision,worldGeneration=world,stateHash=hash,
   units=new[]{new MpUnit{id=99,parent=4,men=932,name="5th Regiment",x=14,y=3,z=27,yaw=90,targetX=17,targetZ=32}},
   towns=new[]{new MpTown{name="Dover",x=9,z=12}}};
 var roundTrip=Wire(state);
 Check(roundTrip.worldGeneration==world && roundTrip.sequence==123 && roundTrip.baseRevision==revision && roundTrip.stateHash==hash,
   "World generation "+world+" survives separately from command and revision fields");
 Check(roundTrip.units.Length==1 && roundTrip.units[0].id==99 && roundTrip.units[0].men==932 && roundTrip.towns[0].name=="Dover",
   "Adding world generation preserves the surrounding state payload");
}
var staleHold=Held();staleHold.worldGeneration=3;
var encodedHold=Wire(staleHold);
Check(encodedHold.worldGeneration==3,"The wire does not silently discard an invalid control-frame world generation");
Reject(()=>NewDrain().Receive(encodedHold),"World generation is checked after real wire decoding");
Console.WriteLine($"Tactical drain: {checks} checks. Native Unity scene loading is not exercised by this suite.");
