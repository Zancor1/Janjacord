# Native video scheduling variant

JanjaCord uses a modified Windows x64 MR-WebRTC 2.0.2 library to support 60 FPS external video sources. Original source: https://github.com/microsoft/MixedReality-WebRTC. The upstream `external_video_track_source.cpp:288` schedules external frame requests at `now + 30` milliseconds, which limited the original binary to about 32 frames/s in a local measurement.

The pinned NuGet DLL has SHA-256 `203a747d06d391c180cba0614fabfc54b16e279e0280e9c0ade2f27c6348d2b5`. Its PE exception table identifies the relevant function range as RVA `0x9b0e0..0x9b2ec`. The instruction at RVA `0x9b2a6`, file offset `0x9a6a6`, is `4d 8d 46 1e` (`lea r8,[r14+30]`), immediately following a reference to the source-path string ending in `external_video_track_source.cpp:288` at VA `0x1806ccf90`.

`Build/patch-video-frame-rate.py` verifies the entire original image hash and instruction bytes, changes that scheduling displacement to 8 ms, removes the obsolete original Authenticode certificate table and updates the PE checksum. The resulting local variant is unsigned and is **not** a Microsoft-signed binary. It retains the same native API and dependencies. The NuGet cache and other installed applications are unchanged. Output SHA-256: `781c97d06b3907f32061ed5f681238e0f0fc8dbfbda2b8a388d2d02ea0eb8447`.

Read-only inspection used REA's PE certificate parser (Evidence `ev_e148e96243c743cce3642a24bdc7d2dbb61513b13b75c4a06fadda314dd795d2`) and PE exception-table mapping plus Capstone instruction decoding. Ghidra's whole-image request exceeded the client's 300-second deadline; no Ghidra function-analysis result is claimed. The analysis session was closed.

The application supplies frames only at the selected 15, 30 or 60 FPS. Extra native polls do not generate duplicate or placeholder frames. The desktop renderer runs at up to60 Hz. Local native-source measurements after modification: 15.1, 30.2 and60.2 FPS respectively, versus approximately31.7 FPS for the original binary when requesting60. This measures native frame delivery, not a guarantee of the same rate at arbitrary resolutions or over arbitrary networks. `ScreenFrameRateProbe` verifies all three rates, and `MediaProbe` checks the receiving WebRTC pipeline.

Original upstream and bundled third-party notices remain applicable; see `WEBRTC_NOTICE.txt` and `THIRD_PARTY_NOTICES.md`.
