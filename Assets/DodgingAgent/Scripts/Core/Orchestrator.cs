using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DodgyBall.Scripts.Core
{
    // Create interface to implement in weapons (moved to its own file)
    // Note all weaponPrefabs in Orchestrator must implement the IWeapon interface
    
    public class Orchestrator : MonoBehaviour
    {
        private struct WeaponEntry // woot organization
        {
            public IWeapon weapon;
            public Transform transform;
        }
        
        [Header("Timing Stuff")]
        public int maxConcurrentSwings = 3;
        [Range(0f, 1f)] public float chanceForConcurrency = 0.25f;
        [Range(0f, 1f)] public float chanceForPrediction = 0.4f;
        public float concurrencyCheckInterval = 0.2f; // Check for concurrent attacks every N seconds
        public float intervalMin = 0.5f;
        public float intervalMax = 1.5f;
        public float durationMin = 0.25f;
        public float durationMax = 1f;
        
        public AnimationCurve rangeShiftCurve = AnimationCurve.Linear(0, -1, 1, 1); // How fast to shift
        [Range(0.01f, 1f)] public float shiftStepPercent = 0.10f; // When to shift % of step
        //TODO: Add a ceiling and floor maybe (How about finish implementing the range shift...)
        
        [Header("Weapons (Prefabs/Scene Objects")]
        [Tooltip("Prefabs to instantiate. Each must include a component that implements IWeapon.")]
        public GameObject[] weaponPrefabs;  // Must implement IWeapon

        [Header("Random Weapon Count")]
        [Tooltip("If true, instantiate a random number of weapons between minWeaponCount and maxWeaponCount.")]
        public bool useRandomWeaponCount = false;
        public int minWeaponCount = 1;
        public int maxWeaponCount = 3;
        
        [Header("Target")]
        public Transform target;
        public float positionRange = 5f;
        private Rigidbody _targetRb;
        
        private readonly List<WeaponEntry> _weapons = new();
        private readonly List<AttackHandler> _waiting = new();
        private readonly List<AttackHandler> _active = new();
        
        // FixedUpdate scheduler state
        private float _intervalTimer = 0f;
        private float _nextInterval = 0f;
        private float _nextConcurrencyCheck = 0f;

        void Awake()
        {
            if (!target) { Debug.LogWarning("Orchestrator: no target assigned."); return; }
            _targetRb = target.GetComponent<Rigidbody>();
            if (!_targetRb) Debug.LogWarning("Orchestrator: target has no Rigidbody. Predictive aiming will be disabled.");
        }

        void Start() { CollectWeapons(); }
        
        void OnEnable()
        {
            if (_weapons.Count == 0) return;
            ResetScheduler();
        }
        
        void OnDisable() { StopAllAttacks(); }
        
        public void ResetScheduler()
        {
            _intervalTimer = 0f;
            _nextInterval  = Random.Range(intervalMin, intervalMax);
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        private void CollectWeapons()
        {
            _weapons.Clear();
            if (weaponPrefabs == null || weaponPrefabs.Length == 0)
            {
                Debug.LogWarning("No weaponPrefabs assigned.");
                return;
            }
            
            int weaponCountToInstantiate = useRandomWeaponCount ? Random.Range(minWeaponCount, maxWeaponCount + 1) : weaponPrefabs.Length;
            for (int i = 0; i < weaponCountToInstantiate; i++)
            {
                var prefab = weaponPrefabs[i % weaponPrefabs.Length];
                if (!prefab) continue;
                
                GameObject initialized = Instantiate(prefab, transform);
                
                IWeapon iWeapon = initialized.GetComponent<IWeapon>() ?? initialized.GetComponentInChildren<IWeapon>(true);
                if (iWeapon == null) { Debug.LogError($"Prefab '{prefab.name}' has no component implementing IWeapon."); continue; }
                initialized.transform.localPosition = Random.insideUnitSphere * positionRange;
                // iWeapon.Orient(target.localPosition); (no more orientation, they should handle their own directions)
                
                _weapons.Add(new WeaponEntry{ weapon = iWeapon, transform = initialized.transform });
            }
            
            // Build weapon _active and _waiting pools
            BuildPools();
        }
        
        private void BuildPools()
        {
            _waiting.Clear();
            _active.Clear();

            foreach (var entry in _weapons)
            {
                var runner = entry.weapon as MonoBehaviour;
                if (!runner)
                {
                    Debug.LogError("IWeapon must also be a MonoBehaviour.");
                    continue;
                }
                // Not attacking yet → put into waiting
                _waiting.Add(new AttackHandler(entry.weapon, runner, routine: null));
            }
        }
        
        // Create attack coroutine using handle, start it, and move to _active list
        // ReSharper disable Unity.PerformanceAnalysis
        private void StartAttack(AttackHandler handle, float duration)
        {
            // Add Prediction if rolled
            var targetPosition = target.localPosition;
            if (_targetRb && Random.value < chanceForPrediction)
            {
                float impactTime = handle.Weapon.GetImpactTime(duration, targetPosition);
                Vector3 predictedOffset = transform.InverseTransformDirection(_targetRb.linearVelocity) * impactTime;
                Vector3 predictedPosition = targetPosition + predictedOffset;
                targetPosition = predictedPosition;
            }

            handle.Start(duration, targetPosition, () =>
            {
                _active.Remove(handle);
                _waiting.Add(handle);
            });
            
            _active.Add(handle);
        }

        private void FixedUpdate()
        {
            if (_weapons.Count == 0 || !target) return;

            _intervalTimer += Time.fixedDeltaTime;
            
            // Handle Attacks
            if (_intervalTimer >= _nextInterval)
            {
                _intervalTimer = 0f;
                _nextInterval  = Random.Range(intervalMin, intervalMax);

                // Start an attack when interval expires (or force start if none active)
                if (_waiting.Count > 0 && _active.Count < maxConcurrentSwings)
                {
                    int idx = Random.Range(0, _waiting.Count);
                    var handle = _waiting[idx];
                    _waiting.RemoveAt(idx);

                    float duration = Random.Range(durationMin, durationMax);
                    StartAttack(handle, duration);
                }
            }

            // Allow concurrent attacks to join in every N seconds
            if (Time.fixedTime >= _nextConcurrencyCheck && _active.Count > 0 && _waiting.Count > 0 && _active.Count < maxConcurrentSwings)
            {
                _nextConcurrencyCheck = Time.fixedTime + concurrencyCheckInterval;

                if (Random.value < chanceForConcurrency)
                {
                    int idx = Random.Range(0, _waiting.Count);
                    var handle = _waiting[idx];
                    _waiting.RemoveAt(idx);

                    float duration = Random.Range(durationMin, durationMax);
                    StartAttack(handle, duration);
                }
            }
        }

        // public float GetInformedDuration()
        // {
        //     float progress = Mathf.Clamp01(_step / (float)(totalSteps - 1));
        //     float bucket = Mathf.Floor(progress / shiftStepPercent) * shiftStepPercent;
        //     float t = (rangeShiftCurve.Evaluate(bucket) + 1f) * 0.5f;
        //     return Mathf.Lerp(durationMin, durationMax, t);
        // }
        
        
        // Might as well add some helpers
        private void CancelAndReturn(AttackHandler handle)
        {
            handle.Cancel();
            _active.Remove(handle);
            _waiting.Add(handle);
        }
        public void StopAllFrom(IWeapon weapon)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                if (_active[i].Weapon == weapon)
                    CancelAndReturn(_active[i]);
        }

        public void StopAllAttacks()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                CancelAndReturn(_active[i]);
        }

        public Transform[] GetWeaponTransforms()
        {
            Transform[] transforms = new Transform[_weapons.Count];
            for (int i = 0; i < _weapons.Count; i++)
            {
                transforms[i] = _weapons[i].transform;
            }
            return transforms;
        }

        public void RedrawRandomWeaponCount()
        {
            if (!useRandomWeaponCount) return;

            StopAllAttacks();

            foreach (var entry in _weapons)
            {
                if (entry.transform) Destroy(entry.transform.gameObject);
            }

            CollectWeapons();
            ResetScheduler();
        }
    }
}
