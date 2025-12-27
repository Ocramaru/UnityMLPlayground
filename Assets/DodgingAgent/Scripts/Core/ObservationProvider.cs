using UnityEngine;

namespace DodgingAgent.Scripts.Core
{
    public class ObservationProvider
    {
        [Header("Core References")]
        public Rigidbody agentBody;
        public Transform agentTransform;
        public Orchestrator orchestrator;
        
        [Header("Toggles")]
        public bool includeSelfState = true;
        public bool includeRaySensor = true;
        public bool includeRadarSensor = false;
        public bool includeWeaponBuffer = false; // original style
        
    }
}