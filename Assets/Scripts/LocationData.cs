using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampusTour
{
    public enum TourMediaType
    {
        Image360,
        Video360
    }

    public enum HotspotType
    {
        Navigation,
        Information
    }

    [Serializable]
    public class HotspotData
    {
        [Tooltip("Type of hotspot: Navigation jumps to another viewpoint, Information displays viewpoint details.")]
        public HotspotType type = HotspotType.Navigation;

        [Tooltip("Zero-based index of the destination location (0 to 5 for navigation).")]
        public int targetLocationIndex = 0;

        [Tooltip("Horizontal angle in degrees (-180 to 180) around the viewer.")]
        [Range(-180f, 180f)]
        public float yaw = 0f;

        [Tooltip("Vertical angle in degrees (-80 to 80) relative to viewer horizon.")]
        [Range(-80f, 80f)]
        public float pitch = 0f;

        [Tooltip("Distance from viewer origin (default: 10 meters).")]
        public float distance = 10f;

        [Tooltip("Descriptive label shown on hover or below the hotspot icon.")]
        public string label = "Go to location";
    }

    [Serializable]
    public class LocationData
    {
        [Tooltip("Human-readable viewpoint title.")]
        public string locationName = "Campus Location";

        [Tooltip("JPG/PNG filename inside Assets/StreamingAssets/Images/ (e.g. Location1.jpg).")]
        public string imageName = "Location1.jpg";

        [TextArea(3, 8)]
        [Tooltip("Detailed description of the campus viewpoint.")]
        public string description = "";

        [Tooltip("Media type: 360 Image or 360 Video.")]
        public TourMediaType mediaType = TourMediaType.Image360;

        [Tooltip("Optional MP4 filename inside Assets/StreamingAssets/Videos/ (e.g. Location1.mp4).")]
        public string videoName = "";

        [Tooltip("Fine-tune initial yaw orientation (rotation in degrees) to face the primary entrance/path.")]
        [Range(-180f, 180f)]
        public float yawOffset = 0f;

        [Tooltip("Hotspots configured for this location.")]
        public List<HotspotData> hotspots = new List<HotspotData>();
    }
}
