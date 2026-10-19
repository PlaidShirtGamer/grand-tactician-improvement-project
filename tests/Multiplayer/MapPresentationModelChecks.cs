using GTIP;

static class MapPresentationModelChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool valid,string name){if(!valid)throw new Exception(name);checks++;}
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(System.IO.InvalidDataException){rejected=true;}Check(rejected,name);}
        var route=new MpPathState{Sample=4,Lengths=new[]{2,1},Status=new[]{2,1},BlockPath=new[]{0,1},BlockCorner=new[]{1,0},
            Corners=new[]{1f,2f,3f,4f,5f,6f,7f,8f,9f},Destinations=new[]{7f,8f,9f,10f,11f,12f},DestinationYaw=new[]{90f,180f}};
        MpWorldRules.Path(route);checks++;
        var schema=MpStateSchema.Scalars(typeof(MpPathState));
        var copy=new MpPathState();schema.Apply(copy,MpStateCodec.Decode(new[]{schema},MpStateCodec.Encode(new[]{schema},new[]{new MpStateRow{Id=42,Values=schema.Capture(route)}}))[0].Values);
        Check(copy.Destinations.SequenceEqual(route.Destinations) && copy.DestinationYaw.SequenceEqual(route.DestinationYaw),"Every host block destination and facing survives the route codec");
        Check(copy.Lengths.SequenceEqual(route.Lengths) && copy.Status.SequenceEqual(route.Status),"Destination markers do not replace the native route or order-delay states");
        copy.Destinations=new[]{1f,2f,3f};Reject(()=>MpWorldRules.Path(copy),"A second block cannot use a missing destination marker");
        copy.Destinations=(float[])route.Destinations.Clone();copy.DestinationYaw=new[]{90f};Reject(()=>MpWorldRules.Path(copy),"Destination marker count must match native blocks");
        copy.DestinationYaw=(float[])route.DestinationYaw.Clone();copy.Destinations[0]=float.NaN;Reject(()=>MpWorldRules.Path(copy),"Invalid destination position rejected");
        copy.Destinations=(float[])route.Destinations.Clone();copy.DestinationYaw[1]=float.PositiveInfinity;Reject(()=>MpWorldRules.Path(copy),"Invalid destination facing rejected");
        copy.DestinationYaw=null;Reject(()=>MpWorldRules.Path(copy),"A missing destination facing array is rejected");
        var buildings=new HashSet<int>{3,8,14};
        var supply=new MpMapSupplyState{Regrouping=new[]{-2,14,3,-1},Depots=new[]{14,3}};
        MpMapSupplyRules.Validate(supply,2,buildings.Contains);checks++;
        var supplySchema=MpStateSchema.Scalars(typeof(MpMapSupplyState));
        var supplyCopy=new MpMapSupplyState();supplySchema.Apply(supplyCopy,MpStateCodec.Decode(new[]{supplySchema},MpStateCodec.Encode(new[]{supplySchema},new[]{new MpStateRow{Id=633,Values=supplySchema.Capture(supply)}}))[0].Values);
        Check(supplyCopy.Regrouping.SequenceEqual(new[]{-2,14,3,-1}),"Town and stable building identities preserve the host regrouping-area order");
        Check(supplyCopy.Depots.SequenceEqual(new[]{14,3}),"Connected supply depot identities survive host replication");
        supplyCopy.Regrouping=new[]{-3};Reject(()=>MpMapSupplyRules.Validate(supplyCopy,2,buildings.Contains),"Unknown regrouping town rejected");
        supplyCopy.Regrouping=new[]{int.MinValue};Reject(()=>MpMapSupplyRules.Validate(supplyCopy,2,buildings.Contains),"Town reference overflow rejected");
        supplyCopy.Regrouping=new[]{7};Reject(()=>MpMapSupplyRules.Validate(supplyCopy,2,buildings.Contains),"Unknown stable building identity rejected");
        supplyCopy.Regrouping=new[]{0};Reject(()=>MpMapSupplyRules.Validate(supplyCopy,2,buildings.Contains),"Null regrouping reference rejected");
        supplyCopy.Regrouping=new[]{14,14};Reject(()=>MpMapSupplyRules.Validate(supplyCopy,2,buildings.Contains),"Duplicate regrouping circle rejected");
        supplyCopy.Regrouping=new[]{3};supplyCopy.Depots=new[]{-1};Reject(()=>MpMapSupplyRules.Validate(supplyCopy,2,buildings.Contains),"Town cannot be used as a connected depot");
        supplyCopy.Depots=new[]{8,8};Reject(()=>MpMapSupplyRules.Validate(supplyCopy,2,buildings.Contains),"Duplicate connected depot rejected");
        supplyCopy.Depots=new[]{999};Reject(()=>MpMapSupplyRules.Validate(supplyCopy,2,buildings.Contains),"Unknown connected depot rejected");
        supplyCopy.Depots=null;Reject(()=>MpMapSupplyRules.Validate(supplyCopy,2,buildings.Contains),"Missing supply topology rejected");
        supplyCopy.Regrouping=Array.Empty<int>();supplyCopy.Depots=Array.Empty<int>();MpMapSupplyRules.Validate(supplyCopy,2,buildings.Contains);checks++;
        Check(supply.Regrouping.SequenceEqual(new[]{-2,14,3,-1}) && supply.Depots.SequenceEqual(new[]{14,3}),"Rejected changes leave the original authoritative display model intact");
        return checks;
    }
}
