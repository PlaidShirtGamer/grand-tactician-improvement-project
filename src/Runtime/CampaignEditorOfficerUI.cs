using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class CampaignEditor
    {
        string officerFirst, officerLast, officerBirth, officerPromotion, officerPortrait;
        int officerRank = 5, officerBranch, officerState;
        Vector2 stateScroll;
        void InitializeOfficer()
        {
            officerFirst = ""; officerLast = ""; officerBirth = "1820-01-01"; officerPortrait = "";
            var date = GameVars.GetCampaignFloatingDateSep();
            officerPromotion = date.year.ToString("0000") + "-" + date.month.ToString("00") + "-" + date.day.ToString("00");
            officerState = 0; officerRank = Math.Min(5, GameVars.commanderranks - 1);
        }
        void DrawNewOfficer()
        {
            GUILayout.Label("Adds an available, unassigned land officer to this campaign. Base-game officer creation and portrait importing are not yet ported.");
            GUILayout.Label("First name"); officerFirst = GUILayout.TextField(officerFirst);
            GUILayout.Label("Last name"); officerLast = GUILayout.TextField(officerLast);
            if (GUILayout.Button("Randomize name")) Run(() => {
                var first = GameVars.alliance[side].commander_firstname;
                var last = GameVars.alliance[side].commander_lastname;
                if (first.Count == 0 || last.Count == 0) throw new InvalidOperationException("No faction name pool is loaded.");
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    officerFirst = first[UnityEngine.Random.Range(0, first.Count)]; officerLast = last[UnityEngine.Random.Range(0, last.Count)];
                    if (!GameVars.commander.Any(c => c != null && c.firstname == officerFirst && c.name == officerLast)) return;
                }
                throw new InvalidOperationException("Could not find a unique random name; edit the name manually.");
            });
            GUILayout.Label("Rank");
            var ranks = GameVars.ranktext.Take(GameVars.commanderranks).ToArray();
            officerRank = GUILayout.SelectionGrid(officerRank, ranks, 3);
            GUILayout.Label("Branch"); officerBranch = GUILayout.Toolbar(officerBranch, new[] { "Infantry", "Cavalry", "Artillery" });
            GUILayout.Label("Home state filter"); optionSearch = GUILayout.TextField(optionSearch);
            officerState = Pick("Home state", GameVars.nation.Select(n => n.name).ToArray(), officerState, ref stateScroll);
            GUILayout.Label("Date of birth (YYYY-MM-DD)"); officerBirth = GUILayout.TextField(officerBirth);
            GUILayout.Label("Date rank attained (YYYY-MM-DD)"); officerPromotion = GUILayout.TextField(officerPromotion);
            GUILayout.Label("Existing portrait filename in Avatar, without .png (blank uses silhouette)"); officerPortrait = GUILayout.TextField(officerPortrait);
            GUILayout.Label("Starting attributes: experience/fame 0%; leadership, initiative, administration and cunning 50%. Select the new officer afterward to edit attributes and background traits.");
        }
        void ReviewNewOfficer()
        {
            string first = (string)LiveEditSession.Name(officerFirst), last = (string)LiveEditSession.Name(officerLast);
            DateTime birth, promotion;
            if (!DateTime.TryParseExact(officerBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out birth) ||
                !DateTime.TryParseExact(officerPromotion, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out promotion))
                throw new InvalidOperationException("Use valid dates in YYYY-MM-DD format.");
            var current = GameVars.GetCampaignFloatingDateSep();
            var today = new DateTime(current.year, current.month, current.day);
            if (birth.Year < 1700 || birth >= promotion || promotion > today || birth.AddYears(16) > promotion)
                throw new InvalidOperationException("Officer must be at least 16 at promotion; promotion must not be later than the campaign date.");
            int rank = officerRank, branch = officerBranch, stateId = officerState, faction = side;
            if (rank < 1 || rank >= GameVars.commanderranks || string.IsNullOrWhiteSpace(GameVars.ranktext[rank]))
                throw new InvalidOperationException("Select a valid officer rank.");
            string portrait = officerPortrait.Trim();
            if (portrait.Length > 0 && (portrait.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || portrait.Contains(".") ||
                !File.Exists(Path.Combine("Avatar", portrait + ".png"))))
                throw new InvalidOperationException("Choose an existing PNG in the game's Avatar folder; enter only its filename without the extension.");
            operationReview = "Create " + GameVars.ranktext[rank] + " " + first + " " + last + " in " + (faction == 0 ? "Union" : "Confederacy") +
                ". Born " + officerBirth + "; rank attained " + officerPromotion + ". Portrait: " + (portrait.Length == 0 ? "silhouette" : portrait) + ".";
            operationApply = () => {
                if (portrait.Length > 0 && !File.Exists(Path.Combine("Avatar", portrait + ".png"))) throw new InvalidOperationException("Portrait was removed; review again.");
                var officer = new GameVars.Commander { firstname = first, name = last, combinedname = first + " " + last,
                    alliance = faction, rank = rank, historicalbranch = branch, stateid = stateId,
                    dateofbirth = new Tools.Date(birth.Day, birth.Month, birth.Year),
                    usedummyphoto = true, portraitfilename = portrait, servicehistory = "Created with GTIP Aide-de-Camp",
                    activeincampaign = true, status = 0, leadership = .5f, initiative = .5f, administration = .5f, cunning = .5f };
                officer.dateofpromotion[rank] = new Tools.Date(promotion.Day, promotion.Month, promotion.Year);
                GameVars.commander.Add(officer);
                domain = 3; search = first + " " + last;
            };
        }
    }
}
