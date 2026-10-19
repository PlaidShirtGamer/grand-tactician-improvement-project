using GTIP;

static class TransientChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
        var receipts=new MpMapNoticeReceipts();
        Check(receipts.Take(0),"Native rolling-text identity zero is a valid first event");
        Check(!receipts.Take(0),"A fast event cannot replay through the next campaign snapshot");
        Check(receipts.Take(4) && receipts.Take(2) && receipts.Take(3),"Slow snapshots may fill unseen notice identities behind a newer fast event");
        Check(!receipts.Take(2),"Reordered duplicate notice is shown once");
        bool rejected=false;try{receipts.Take(-1);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"Malformed native notice identities fail closed");
        receipts.Clear();
        Check(receipts.Take(0),"A fresh campaign can reuse the native notice counter");
        for(int i=1;i<10000;i++)receipts.Take(i);
        Check(receipts.Count<=MpMapNoticeReceipts.Window,"Long-running combat bounds receipt storage independently of event count");
        Check(!receipts.Take(0) && !receipts.Take(9999),"Both evicted history and the newest duplicate remain consumed");
        Check(receipts.Take(10001) && receipts.Take(10000),"Out-of-order events remain accepted after receipt-window eviction");
        receipts.Clear();receipts.Take(1);receipts.Take(1000000);
        Check(!receipts.Take(2) && receipts.Take(999999),"A large event gap rejects stale history but retains the bounded reorder tail");
        receipts.Clear();receipts.Take(int.MaxValue);
        Check(!receipts.Take(0) && receipts.Take(int.MaxValue-1),"Notice age-window arithmetic cannot overflow at the native integer limit");
        receipts.Clear();
        Check(receipts.Count==0 && receipts.Take(1),"Session cleanup clears both high-water mark and duplicate receipts");

        Check(MpDispatchSymbols.Matches("005_Symbol",5,0),"First native transient dispatch symbol is recognized");
        Check(MpDispatchSymbols.Matches("046_Symbol",6,40),"Native dispatch symbol offsets are included in identity");
        Check(!MpDispatchSymbols.Matches("004_Symbol",4,0) && !MpDispatchSymbols.Matches("047_Symbol",47,0),"Map icons outside the native dispatch range are excluded");
        Check(!MpDispatchSymbols.Matches("020_Symbol",21,0),"A rolling map icon with a coincident numeric event identity cannot impersonate another dispatch type");
        Check(!MpDispatchSymbols.Matches("020_Symbol",int.MaxValue,int.MaxValue),"Overflowing icon offsets cannot match a dispatch");
        Check(!MpDispatchSymbols.Matches(null,5,0) && !MpDispatchSymbols.Matches("5_Symbol",5,0) && !MpDispatchSymbols.Matches("005_SymbolExtra",5,0),"Only the exact native symbol name format is eligible");
        Check(!MpDispatchSymbols.Matches("bad_Symbol",5,0) && !MpDispatchSymbols.Matches("005_Button",5,0),"Malformed or ordinary UI buttons cannot enter native dispatch interaction");
        return checks;
    }
}
