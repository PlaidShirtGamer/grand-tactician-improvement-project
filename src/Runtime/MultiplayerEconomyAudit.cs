using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        void AuditEconomy(MpStateRow[] rows,int revision,bool readback)
        {
            if(diagnosticPath==null || (revision!=1 && revision%30!=0))return;
            var csv=new StringBuilder("revision,kind,id,field,value\n");
            foreach(var row in rows.Where(r=>r.Kind==14 || r.Kind==16 || r.Kind==31 || r.Kind==32 || r.Kind==33)) {
                var schema=DataSchemas[row.Kind];object[] values=row.Values;
                if(readback) {
                    int n=GameVars.alliance.Length;
                    if(row.Kind==14)values=schema.Capture(Economy.resources[row.Id/n].productalliancedata[row.Id%n]);
                    else if(row.Kind==16 || row.Kind==31) {
                        var chart=row.Kind==16?Economy.goodschartref:Economy.financeschartref;var d=chart.chartdatabase[row.Id];
                        values=schema.Capture(new MpChartPoints{Time=d.time.ToArray(),Value=d.value.ToArray()});
                    } else if(row.Kind==33)values=schema.Capture(row.Id%2==0?GameVars.alliance[row.Id/2].constructioncostspa:GameVars.alliance[row.Id/2].weaponcostspa);
                    else {
                        int record=row.Id/2;var d=Economy.resources[record/n/n].productalliancedata[(record/n)%n];
                        values=schema.Capture(row.Id%2==0?d.importvalue[record%n]:d.exportvalue[record%n]);
                    }
                    string mismatch=MpStateBaseline.FirstDifference(schema,row.Values,values);
                    if(mismatch!=null)throw new InvalidDataException("Economy readback diverged: "+row.Kind+":"+row.Id+" "+mismatch);
                }
                for(int i=0;i<values.Length;i++) {
                    string text=values[i] is float[] array?string.Join(";",array.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)).ToArray()):Convert.ToString(values[i],CultureInfo.InvariantCulture);
                    csv.Append(revision).Append(',').Append(row.Kind).Append(',').Append(row.Id).Append(',').Append(schema.Fields[i].Name).Append(',').Append(text).Append('\n');
                }
            }
            try{File.WriteAllText(diagnosticPath+".economy.csv",csv.ToString());}
            catch(IOException error){LogDiagnostic("Economy audit unavailable: "+error.Message);}
        }
    }
}
