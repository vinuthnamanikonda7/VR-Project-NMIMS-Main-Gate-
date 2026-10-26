using System;
using UnityEngine;

namespace CampusTour
{
    public enum HotspotType
    {
        Navigation,
        Info
    }

    [Serializable]
    public class HotspotData
    {
        [Tooltip("Type of hotspot: Navigation to another location, or Info popup.")]
        public HotspotType type = HotspotType.Navigation;

        [Tooltip("Title or label shown on hover or in info popup.")]
        public string title = "Next Area";

        [TextArea(2, 5)]
        [Tooltip("Descriptive text shown if type is Info.")]
        public string description = "";

        [Tooltip("Target location index in CampusTourManager locations list (if type is Navigation).")]
        public int targetLocationIndex = 0;

        [Range(-180f, 180f)]
        [Tooltip("Horizontal angle in degrees around the camera (-180 to 180).")]
        public float yaw = 0f;

        [Range(-85f, 85f)]
        [Tooltip("Vertical angle in degrees (-85 looking down, 85 looking up).")]
        public float pitch = 0f;

        [Tooltip("Distance from the camera in meters.")]
        public float distance = 10f;
    }
}
