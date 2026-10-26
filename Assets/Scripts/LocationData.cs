using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampusTour
{
    [Serializable]
    public class LocationData
    {
        [Tooltip("Display name of the campus location.")]
        public string name = "New Location";

        [Tooltip("Media file name located inside StreamingAssets/Media/ or StreamingAssets/Videos/ (e.g. Location1.jpg or Gate.mp4).")]
        public string mediaFileName = "Location1.jpg";

        [TextArea(2, 6)]
        [Tooltip("General overview or history of this location.")]
        public string description = "Welcome to this campus spot.";

        [Tooltip("Interactive hotspots visible at this location.")]
        public List<HotspotData> hotspots = new List<HotspotData>();
    }
}
