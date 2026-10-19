using System;
using System.Collections.Generic;
using System.IO;

namespace GTIP
{
    public sealed class MpMapSupplyState
    {
        // Buildings use stable positive session IDs. Towns use -(native index+1).
        // Preserve the host's order, which is also the native regrouping-range order.
        public int[] Regrouping,Depots;
    }
    public static class MpMapSupplyRules
    {
        public static void Validate(MpMapSupplyState value,int townCount,Func<int,bool> buildingExists)
        {
            if(value==null || value.Regrouping==null || value.Depots==null || value.Regrouping.Length>20000 || value.Depots.Length>20000)
                throw new InvalidDataException("Invalid native supply display references.");
            var seen=new HashSet<int>();
            foreach(int id in value.Regrouping) {
                if(id==0 || !seen.Add(id) || (id>0?!buildingExists(id):id==int.MinValue || -id>townCount))
                    throw new InvalidDataException("Unknown native regrouping location.");
            }
            seen.Clear();
            foreach(int id in value.Depots)if(id<=0 || !seen.Add(id) || !buildingExists(id))throw new InvalidDataException("Unknown native supply depot.");
        }
    }
}
