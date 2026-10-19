using System;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        float pacingSource=-1,pacingArrival=-1,pacingNextLog;
        float pacingSourceMax,pacingArrivalMax;
        double pacingSourceTotal,pacingArrivalTotal;
        int pacingIntervals,pacingFrames,pacingSamples,motionQueueDeferrals;
        long pacingEvaluations;
        void ObserveMotionPacing(float source,float arrival,bool paused,int samples)
        {
            pacingFrames++;pacingSamples+=samples;
            if(paused)pacingSource=pacingArrival=-1;
            else {
                float sourceGap=source-pacingSource,arrivalGap=arrival-pacingArrival;
                // A stationary formation stream legitimately stops sending. Exclude
                // pauses and long sparse gaps; log the continuous moving cadence.
                if(pacingSource>=0 && sourceGap>0 && sourceGap<1 && arrivalGap>=0) {
                    pacingIntervals++;pacingSourceTotal+=sourceGap;pacingArrivalTotal+=arrivalGap;
                    pacingSourceMax=Math.Max(pacingSourceMax,sourceGap);pacingArrivalMax=Math.Max(pacingArrivalMax,arrivalGap);
                }
                pacingSource=source;pacingArrival=arrival;
            }
            if(pacingNextLog==0){pacingNextLog=arrival+15;return;}
            if(arrival<pacingNextLog)return;
            double sourceAverage=pacingIntervals==0?0:pacingSourceTotal/pacingIntervals;
            double arrivalAverage=pacingIntervals==0?0:pacingArrivalTotal/pacingIntervals;
            LogDiagnostic("Motion pacing: role="+(host?"host":"guest")+", frames="+pacingFrames+", poses="+pacingSamples+
                ", sourceAvgMs="+(sourceAverage*1000).ToString("F1")+", sourceMaxMs="+(pacingSourceMax*1000).ToString("F1")+
                ", arrivalAvgMs="+(arrivalAverage*1000).ToString("F1")+", arrivalMaxMs="+(pacingArrivalMax*1000).ToString("F1")+
                ", visualEvaluations="+(poseEvaluations-pacingEvaluations)+", cachedRoots="+motionRoots.Count+", queueDeferrals="+motionQueueDeferrals);
            pacingFrames=pacingSamples=pacingIntervals=motionQueueDeferrals=0;pacingSourceTotal=pacingArrivalTotal=0;
            pacingSourceMax=pacingArrivalMax=0;pacingEvaluations=poseEvaluations;pacingNextLog=arrival+15;
        }
        void ClearMotionPacing()
        {
            pacingSource=pacingArrival=-1;pacingNextLog=0;pacingSourceMax=pacingArrivalMax=0;
            pacingSourceTotal=pacingArrivalTotal=0;pacingIntervals=pacingFrames=pacingSamples=motionQueueDeferrals=0;pacingEvaluations=0;
        }
    }
}
