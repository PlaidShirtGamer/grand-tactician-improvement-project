using System;
using System.IO;
using System.Linq;
using UnityEngine;
using TMPro;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        public static void LoadHotkeys(GameObject[] lines)
        {
            var file=File.ReadAllLines("config/hotkeys.dat");
            if(file.Length==0 || !int.TryParse(file[0],out int count) || count<0 || count>1000 || file.Length<count+1)throw new InvalidDataException("Invalid hotkey configuration.");
            // Consume every file entry, even when an Options panel has no UI object for
            // that index. All three native Options panels share this global key table.
            int size=Math.Max(100,Math.Max(count,lines?.Length??0));
            var keys=new GameVars.KeyCodes[size];
            for(int i=0;i<size;i++) {
                var go=lines!=null && i<lines.Length?lines[i]:null;
                var label=go==null?null:go.GetComponent<TextMeshProUGUI>();
                var code=i<count && Enum.TryParse(file[i+1],out KeyCode parsed)?parsed:KeyCode.None;
                keys[i]=new GameVars.KeyCodes(label==null?"":label.text,go,code);
            }
            GameVars.keycodes=keys;
        }
        void LogHotkeys()
        {
            if(GameVars.keycodes==null){LogDiagnostic("Hotkeys: table unavailable.");return;}
            string Binding(int index)=>index>=0 && index<GameVars.keycodes.Length && GameVars.keycodes[index]!=null?GameVars.keycodes[index].keycode.ToString():"missing";
            LogDiagnostic("Hotkeys: menu="+Binding(GameVars.key_showmenu)+", pause="+Binding(GameVars.key_pausegame)+", camera="+string.Join(",",new[]{GameVars.key_mapleft,GameVars.key_mapright,GameVars.key_mapforward,GameVars.key_mapback}.Select(Binding))+", speed="+string.Join(",",new[]{GameVars.key_speed0,GameVars.key_speed1,GameVars.key_speed2,GameVars.key_speed3,GameVars.key_speed4}.Select(Binding)));
        }
    }
}
