# SVKM NMIMS Campus VR Tour

A high-fidelity, interactive **360° Virtual Reality Campus Tour** built for **Unity 6 (6000.3)** and the **Universal Render Pipeline (URP)**, supporting **Meta Quest 2, Quest 3, Quest 3S**, desktop Unity Editor simulation, and standard Android touch devices.

---

## 1. Project Overview

The **SVKM NMIMS Campus VR Tour** lets users explore six viewpoints around the SVKM NMIMS campus main gate using high-resolution 360-degree GoPro photographs:

- **Location 1 — Left Side Outside the Main Gate** (`Location1.jpg`)
- **Location 2 — Right Side Outside the Main Gate** (`Location2.jpg`)
- **Location 3 — Full View Outside the Main Gate** (`Location3.jpg`)
- **Location 4 — Left Side Inside the Main Gate** (`Location4.jpg`)
- **Location 5 — Right Side Inside the Main Gate** (`Location5.jpg`)
- **Location 6 — Full View Inside the Main Gate** (`Location6.jpg`)

### Non-VR Panorama Mode vs. True Headset VR

- **Editor & Standard Mobile Mode:**
  - Operates via the **Unity New Input System**.
  - On PC/Editor: Click-and-drag with the mouse rotates pitch and yaw smoothly. Pitch is clamped (-85° to +85°) to prevent camera flipping.
  - On Android Phone: Single-finger touch drag rotates the panoramic view. Taps on UI controls or interactive hotspots do not trigger unwanted scene rotation.
  - Uses an overlay canvas for easy navigation, viewpoint switching, and informational dialogs.

- **True Meta Quest VR Mode:**
  - Runs directly on the Meta Quest headset as a standalone Android APK.
  - Leverages genuine stereo rendering (separate viewports for each eye) and 6DOF/3DOF hardware head tracking via OpenXR.
  - Mouse and touch look rotation are automatically disabled in VR so they never conflict with natural physical head movements.
  - Provides hands-free **Gaze Dwell Selection** (focusing on a hotspot for 1.4 seconds with a visual progress ring) and XR raycast selection.

---

## 2. GoPro 360° Photographs

The application streams equirectangular 360° images directly from `Assets/StreamingAssets/Images/`:

- `Assets/StreamingAssets/Images/Location1.jpg`
- `Assets/StreamingAssets/Images/Location2.jpg`
- `Assets/StreamingAssets/Images/Location3.jpg`
- `Assets/StreamingAssets/Images/Location4.jpg`
- `Assets/StreamingAssets/Images/Location5.jpg`
- `Assets/StreamingAssets/Images/Location6.jpg`

### Image Format & Technical Guidelines

- **Format:** Genuine equirectangular 360° JPG or PNG.
- **Aspect Ratio:** Exact 2:1 aspect ratio (e.g. `3840 × 1920`, `5760 × 2880`, or `4096 × 2048`).
- **Orientation Tuning:** Each location has a configurable `yawOffset` in `CampusTourManager` to align the default forward view with the main entrance or pathway without rotating the physical camera.
- **Handling Missing Files:** If a file is absent or corrupted, the system presents a friendly notice panel (`360° image not found: LocationX.jpg`) rather than crashing.

---

## 3. Automated Scene Builder

The project includes an automated Editor Scene Builder:

1. In the Unity Editor top menu, select:
   ```text
   Tools -> Campus VR Tour -> Build Scene
   ```
2. The builder will automatically:
   - Create and configure `Assets/Scenes/CampusTour.unity`.
   - Setup the panoramic camera rig with URP settings and `TrackedPoseDriver`.
   - Instantiate the procedural inward-facing panoramic sphere and `PanoramaRenderer`.
   - Setup the UI hierarchy (Top Bar, Location Banner, Bottom Navigation Bar, Locations Modal, Info Modal, Fade Transition Overlay, Gaze Reticle).
   - Configure the EventSystem with `InputSystemUIInputModule`.
   - Register the 6 default viewpoints and navigation hotspots.
   - Safely add `Assets/Scenes/CampusTour.unity` to `EditorBuildSettings.scenes` while preserving all other scenes in the project.
3. Open `Assets/Scenes/CampusTour.unity` to inspect or run the tour.

---

## 4. Testing in the Unity Editor

1. Open `Assets/Scenes/CampusTour.unity`.
2. Press **Play** in the Unity Editor.
3. **Look Around:** Click and drag the left mouse button across the Game view to pan in all 360 degrees.
4. **Navigation Hotspots:**
   - Hover over blue navigation arrows (`➜`) to see destination labels.
   - Click a navigation arrow to transition to the linked viewpoint with a smooth 0.5s fade-to-black.
5. **Information Hotspot:** Click any green info icon (`ℹ`) or the bottom **Viewpoint Info** button to open the location details popup.
6. **Locations Menu:** Click **☰ Locations** to open the full list of all 6 viewpoints and jump directly to any viewpoint.
7. **Reset View:** Click **↺ Reset View** to center the camera orientation.
8. **Toggle UI:** Click **👁 Toggle UI** to hide the UI overlay for an unobstructed view of the panorama.

---

## 5. Building a Normal Android APK

1. In Unity, open **File -> Build Profiles** (or **Build Settings**).
2. Select **Android** platform (click *Switch Platform* if not already active).
3. Confirm settings in **Project Settings -> Player -> Android**:
   - **Scripting Backend:** `IL2CPP`
   - **Target Architectures:** `ARM64` checked (ARMv7 unchecked)
   - **Minimum API Level:** Android 10.0 (API Level 29) or higher
   - **Orientation:** Landscape Left
4. Ensure `Assets/Scenes/CampusTour.unity` is included in the build scene list.
5. Click **Build** to generate `SVKM_NMIMS_Tour.apk`.

---

## 6. Building for Meta Quest (Quest 2, 3, 3S)

To deploy as a standalone VR APK on Meta Quest:

### OpenXR & XR Management Setup
1. Open **Edit -> Project Settings -> XR Plug-in Management**.
2. Select the **Android tab** (robot icon).
3. Ensure **OpenXR** is enabled under Plug-in Providers.
4. Under **OpenXR Settings (Android)**:
   - Enable the **Meta Quest Feature Group** (or **Meta Quest Support**).
   - Under *Interaction Profiles*, add **Oculus Touch Controller Profile** and **Meta Quest Touch Plus Controller Profile**.
5. Ensure **Render Mode** is set to **Single Pass Instanced** (Multiview).

### Building and Installing the APK
1. In **File -> Build Profiles**, ensure `CampusTour.unity` is the first scene (or selected scene).
2. Connect your Meta Quest headset via USB-C. Ensure Developer Mode is enabled in the Meta Quest mobile app.
3. Run:
   ```bash
   adb devices
   ```
   (Verify your Quest headset is listed as authorized).
4. Click **Build and Run** in Unity, or build the APK and install via ADB:
   ```bash
   adb install -r SVKM_NMIMS_Tour.apk
   ```
5. On the Quest headset, open the App Library, select the dropdown filter in the top right, choose **Unknown Sources**, and launch **SVKM NMIMS Campus VR Tour**.

---

## 7. Optional 360° Videos

The tour includes full architecture for equirectangular 360° MP4 videos:

1. Place MP4 files into `Assets/StreamingAssets/Videos/` (e.g. `GateVideo.mp4`).
2. In `CampusTourManager` (in inspector or data definition), change a location's `mediaType` to `TourMediaType.Video360` and set `videoName` to `GateVideo.mp4`.
3. When navigating to this location:
   - The video streams to a high-resolution 3840×1920 RenderTexture.
   - Video controls (Play/Pause, Mute/Unmute) automatically appear above the bottom navigation bar.

---

## 8. Troubleshooting Guide

### Black Panorama / Missing Image
- **Cause:** Photograph file is missing or filename has a typo.
- **Fix:** Verify files exist in `Assets/StreamingAssets/Images/` with exact names (`Location1.jpg` to `Location6.jpg`). Check that file extensions match (case-sensitive on Android).

### Panorama Flipped / Backwards
- **Cause:** Standard sphere UVs are mapped for exterior viewing.
- **Fix:** `PanoramaRenderer.cs` procedurally generates an inward-facing mesh with mirrored horizontal UVs (`1 - u`). This ensures all text and building facades appear in their true real-world orientation.

### Stretched Image / Distorted Perspective
- **Cause:** Photograph is not an equirectangular panorama with a 2:1 ratio.
- **Fix:** GoPro MAX / Fusion 360 exports should be stitched in GoPro Player with *Equirectangular 360* projection at 3840×1920 or 5760×2880.

### Pink / Missing Material
- **Cause:** Standard built-in shader assigned in Universal Render Pipeline.
- **Fix:** The project includes `CampusTour/Panorama360.shader` specifically written in HLSL for URP with single-pass stereo instancing support and fallback to `Universal Render Pipeline/Unlit`.

### APK Shows Flat Screen Instead of VR on Headset
- **Cause:** Android build was packaged without the OpenXR loader active in XR Plug-in Management.
- **Fix:** Verify in **Project Settings -> XR Plug-in Management -> Android** that OpenXR is checked and Meta Quest feature is enabled.

### UI Blocked or Hard to Read
- **Cause:** Panorama background contrast.
- **Fix:** All UI panels use high-contrast dark translucent backgrounds (`#10141E`, 85% opacity) with crisp white text and restrained blue accents (`#1F74E0`). Click **👁 Toggle UI** to toggle the HUD off/on at any time.

### High Memory Usage on Headset
- **Cause:** Large uncompressed texture dimensions.
- **Fix:** `PanoramaRenderer.cs` automatically disposes and unloads the previous viewpoint's texture before allocating the next one. For optimal Quest memory performance, 4K (3840×1920) or 5.7K equirectangular textures are recommended.
