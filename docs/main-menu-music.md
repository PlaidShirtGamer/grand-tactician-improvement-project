# Main-menu music

The installer includes `Ride to Death` and places it at `GTIP/Audio/main-menu.mp3` in the game installation folder. No separate audio download or manual copy is required. The source asset is tracked at `assets/audio/main-menu.mp3` and is also included in the build-kit ZIP.

The supplied `Ride to Death.mp3` is stored unchanged (1,786,320 bytes; SHA-256 `368B10C209D23C3146267049C903503D6E9DF19FD504109784753B0948D29196`). Its MPEG frames identify 44.1 kHz stereo audio, approximately 148.95 seconds long. The installer embeds those same bytes and verifies them in its standalone self-test.

Installation backs up any existing `GTIP/Audio/main-menu.mp3` before replacing it. Uninstall restores that file, or removes the installed MP3 if none existed. Updating an older five-file installation preserves its original backups and captures any existing unmanaged MP3 before the first managed replacement. Failed or interrupted installs restore the prior files and installer state.

Restart the game after adding or replacing the installed file. A missing or unreadable replacement falls back to the original track; failures are recorded in Unity's player log. Temporarily renaming it restores original music on restart, but restore its installed name and bytes before running an installer update or uninstall: the installer checks managed files for unexpected edits.

Native menu playback follows `MainMenuControls.Update` → `LoadingScreen.JustPlay(4)` → the loading sound engine's purpose 2, a loop using track 31 (`ST_BlackestCrow`). That same bundled resource appears in other loading/campaign playlists. The override therefore supplies the replacement only during native menu-purpose clip selection, then restores the engine's original clip-table reference. Native music volume, mute, fades, looping and source activation remain in charge of playback. Other sound purposes use their original selection.

The local file loads asynchronously through [Unity's audio request API](https://docs.unity.cn/2021.3/Documentation/ScriptReference/Networking.UnityWebRequestMultimedia.html). Once ready, an already active main-menu source can switch to the replacement; a file finishing loading during a campaign does not start menu audio there. The native resource assets are not edited. This is a local cosmetic option and does not change the multiplayer protocol or campaign state.

Verification: 31 focused cases pass against the production loader/selection bridge, including missing/bad files, local URI escaping, asynchronous readiness, context isolation, native argument forwarding, exception-safe table restoration and request/clip cleanup. The full native suite passes 43 schemas / 775 fields, including 19 menu-music contracts. Staging verifies 131 hooks/changes and 117 preserved native methods. These checks double Unity decoding/playback and do not establish audible output in a running game.

Distribution verification: 36 installer checks pass, including real-game compilation, installation of the embedded audio, repeat installation, uninstall, legacy migration, and rollback. The standalone Windows installer self-test verifies the audio's exact size and hash; the build-kit ZIP contains the same checked asset and its checksum.

Both desktop test copies have the same installed patched game SHA-256 `8C8BAB582E89D8438B0B26A2285EBC13F81A20043D57108B2837750D74F7C4F0` and runtime `9DE39A891BDDAA171F83721C8A95D17BE1BB008A2802538C3D9820FD18782B5E`. Both local MP3 copies match the supplied source hash above. Existing installer backups are retained. This optional cosmetic addition keeps the MP v0.39/protocol 30 identity and adds no replicated fields.

Connected/visual acceptance: open the main menu, verify the supplied track and music volume/mute controls, then load a campaign and return to the menu. Confirm loading/campaign tracks follow their existing playlists and the menu returns to the supplied track. A decoder failure should leave stock playback available and identify the local file in the player log.
