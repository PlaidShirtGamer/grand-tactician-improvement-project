using System;
using TMPro;
using UnityEngine;

namespace GTIP
{
    public static class MenuStatus
    {
        public const string Version = "0.1.1";
        public const string Label = "GTIP traversal prototype loaded | v" + Version;
        private static TextMeshProUGUI status;

        // Called after the game's own menu activation, including on return to menu.
        public static void SetVisible(bool visible)
        {
            try
            {
                if (status == null && visible)
                {
                    var versionObject = GameObject.Find("UICampaign/VersionID");
                    if (versionObject == null)
                    {
                        Debug.LogWarning("[GTIP] Runtime loaded, but main-menu VersionID was not found.");
                        return;
                    }
                    var versionText = versionObject.GetComponent<TextMeshProUGUI>();
                    if (versionText == null) return;
                    var labelObject = new GameObject("GTIP_LoadedStatus", typeof(RectTransform));
                    labelObject.layer = versionObject.layer;
                    labelObject.transform.SetParent(versionObject.transform, false);
                    status = labelObject.AddComponent<TextMeshProUGUI>();
                    status.font = versionText.font;
                    status.fontSharedMaterial = versionText.fontSharedMaterial;
                    status.fontSize = Mathf.Clamp(versionText.fontSize, 12f, 16f);
                    status.enableAutoSizing = false;
                    status.enableWordWrapping = false;
                    status.overflowMode = TextOverflowModes.Overflow;
                    status.alignment = TextAlignmentOptions.BottomLeft;
                    status.color = new Color(0.75f, 0.9f, 0.65f, 1f);
                    status.raycastTarget = false;
                    status.text = Label;
                    var rect = status.rectTransform;
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 0f);
                    rect.anchoredPosition = new Vector2(0f, 4f);
                    rect.sizeDelta = new Vector2(500f, 24f);
                    Debug.Log("[GTIP] " + Label + "; menu hook active.");
                }
                if (status != null) status.gameObject.SetActive(visible);
            }
            catch (Exception error)
            {
                // A cosmetic indicator must not interrupt menu startup.
                Debug.LogWarning("[GTIP] Could not display runtime status: " + error);
            }
        }
    }
}
