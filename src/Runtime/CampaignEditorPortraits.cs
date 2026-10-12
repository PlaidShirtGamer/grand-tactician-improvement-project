using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class CampaignEditor
    {
        string[] portraitFiles = new string[0];
        string portraitFilter = "", previewName, portraitError;
        int portraitPage;
        Texture2D portraitPreview;
        void ClearPortraitPreview()
        {
            if (portraitPreview != null) Destroy(portraitPreview);
            portraitPreview = null; previewName = null; portraitError = null;
        }
        void LoadPortraitCatalog()
        {
            ClearPortraitPreview(); portraitFilter = ""; portraitPage = 0;
            string folder = Path.Combine(Environment.CurrentDirectory, "Avatar");
            portraitFiles = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.png").Select(Path.GetFileNameWithoutExtension)
                .Where(n => !n.Contains(".")).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray() : new string[0];
        }
        void PreviewPortrait(string name)
        {
            ClearPortraitPreview(); previewName = name;
            if (string.IsNullOrWhiteSpace(name)) return;
            try
            {
                if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(".")) throw new InvalidOperationException("Select a portrait from the list.");
                var bytes = EditorPortraitFiles.Read(Path.Combine("Avatar", name + ".png"));
                portraitPreview = new Texture2D(2, 2);
                if (!ImageConversion.LoadImage(portraitPreview, bytes, true)) throw new InvalidOperationException("The PNG could not be decoded.");
            }
            catch (Exception error)
            {
                if (portraitPreview != null) Destroy(portraitPreview);
                portraitPreview = null; portraitError = error.Message;
            }
        }
        void DrawPortraitPicker()
        {
            GUILayout.Label("Portrait — " + (string.IsNullOrWhiteSpace(officerPortrait) ? "Silhouette" : officerPortrait));
            if (previewName != officerPortrait) PreviewPortrait(officerPortrait);
            var rect = GUILayoutUtility.GetRect(140, 180, GUILayout.ExpandWidth(false));
            if (portraitPreview != null) GUI.DrawTexture(rect, portraitPreview, ScaleMode.ScaleToFit);
            else GUI.Box(rect, portraitError ?? "Silhouette\n(no custom portrait)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Use silhouette")) { officerPortrait = ""; PreviewPortrait(""); }
            if (GUILayout.Button("Refresh portraits")) Run(LoadPortraitCatalog);
            GUILayout.EndHorizontal();
            GUILayout.Label("Find portrait in Avatar folder");
            string filter = GUILayout.TextField(portraitFilter);
            if (filter != portraitFilter) { portraitFilter = filter; portraitPage = 0; }
            var filtered = portraitFiles.Where(n => n.IndexOf(portraitFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            int pages = Math.Max(1, (filtered.Length + 7) / 8); portraitPage = Math.Min(portraitPage, pages - 1);
            foreach (var name in filtered.Skip(portraitPage * 8).Take(8))
                if (GUILayout.Button((officerPortrait == name ? "✓ " : "") + name)) { officerPortrait = name; PreviewPortrait(name); }
            GUILayout.BeginHorizontal();
            GUI.enabled = portraitPage > 0; if (GUILayout.Button("Previous")) portraitPage--;
            GUI.enabled = true; GUILayout.Label((portraitPage + 1) + " / " + pages + " · " + filtered.Length + " portraits");
            GUI.enabled = portraitPage + 1 < pages; if (GUILayout.Button("Next")) portraitPage++;
            GUI.enabled = true; GUILayout.EndHorizontal();
            GUILayout.Label("To add an image, place its PNG in the game's Avatar folder and refresh. Maximum 4096 × 4096 pixels and 10 MB. The image stays in the installation; share it alongside the save.");
        }
    }
}
