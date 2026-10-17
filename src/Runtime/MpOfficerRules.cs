using System;

namespace GTIP
{
    public static class MpOfficerRules
    {
        public static void Promote(int rank,int expectedRank,int maximumRank,int status,bool belowPlayer)
        {
            if(rank!=expectedRank)throw new InvalidOperationException("Officer rank changed; wait for the updated officer card.");
            if(status>0 || !belowPlayer || rank>=maximumRank)throw new InvalidOperationException("Native policy or active-duty rules prohibit this promotion.");
        }
    }
}
