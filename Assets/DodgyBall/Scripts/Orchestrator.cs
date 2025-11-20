using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DodgyBall.Scripts
{
    // Create interface to implement in weapons
    // Note all weaponPrefabs in Orchestrator must implement the IWeapon interface
    public interface IWeapon
    {
        Coroutine Attack(float duration, Action onComplete);
        Coroutine Attack(float duration, Transform target, Action onComplete);
        Vector3 GetRandomInRangePosition(Transform target);
        void Orient(Transform target);
        bool CannotReach(Vector3 position);
        void ApproachTarget(Vector3 targetPosition);
        void StopApproaching();
    }
    
    public sealed class AttackHandle
    {
        public IWeapon Weapon { get; }
        public MonoBehaviour Runner { get; }
        public Coroutine Routine { get; private set; }
        public bool IsActive => Routine != null;

        public AttackHandle(IWeapon weapon, MonoBehaviour runner, Coroutine routine)
        {
            Weapon = weapon;
            Runner = runner;
            Routine = routine;
        }
        
        // I'll just leave both so either way works
        public void Start(Coroutine routine) => Routine = routine;
        public void Start(float duration, Action onFinished)
        {
            Routine = Weapon.Attack(duration, () =>
            {
                Complete();
                onFinished?.Invoke();
            });
        }
        public void Start(float duration, Transform target, Action onFinished)
        {
            Routine = Weapon.Attack(duration, target, () =>
            {
                Complete();
                onFinished?.Invoke();
            });
        }
        public void Complete() => Routine = null;
        
        public void Cancel()
        {
            if (Routine == null) return;
            Runner.StopCoroutine(Routine);
            Routine = null;
        }
    }
    
    public class Orchestrator : MonoBehaviour
    {
        private struct WeaponEntry // woot organization
        {
            public IWeapon weapon;
            public Transform transform;
        }

        [Header("Attack Options")] 
        public bool useTeleports = false; //Unimplemented Would Be Sick Tho
        public bool disableMovement = false;
        
        [Header("Agent Stuff (here for now)")] public int totalSteps = 100;
        private int _step = 0;
        
        [Header("Timing Stuff")] 
        public int maxConcurrentSwings = 3;
        [Range(0f, 1f)] public float chanceForConcurrency = 0.25f;
        public float intervalMin = 0.5f;
        public float intervalMax = 1.5f;
        public float durationMin = 0.25f;
        public float durationMax = 1f;
        
        public AnimationCurve rangeShiftCurve = AnimationCurve.Linear(0, -1, 1, 1); // How fast to shift
        [Range(0.01f, 1f)] public float shiftStepPercent = 0.10f; // When to shift % of step
        
        //TODO: Add a ceiling and floor maybe
        
        // We should implement a curve to handle how time will impact duration min max so we start off with slower durations
        
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
        
        private readonly List<WeaponEntry> _weapons = new();
        private readonly List<AttackHandle> _waiting = new();
        private readonly List<AttackHandle> _active = new();
        
        // FixedUpdate scheduler state
        private float _intervalTimer = 0f;
        private float _nextInterval = 0f;

        void Awake()
        {
            if (!target) { Debug.LogWarning("Orchestrator: no target assigned."); return; }
            if (!SwingKeyframeSet.IsLoaded) SwingKeyframeSet.LoadSingleton(); // Load the sword keyframe helper
            Debug.Log($"Loaded {SwingKeyframeSet.Instance.Count} keyframes.");
        }

        void Start()
        {
            CollectWeapons();
        }
        
        void OnEnable()
        {
            if (_weapons.Count == 0) return;
            ResetScheduler();
        }
        
        void OnDisable()
        {
            StopAllAttacks();
        }
        
        public void ResetScheduler()
        {
            _intervalTimer = 0f;
            _nextInterval  = Random.Range(intervalMin, intervalMax);
        }
        
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
                initialized.transform.localPosition = iWeapon.GetRandomInRangePosition(target);
                iWeapon.Orient(target);
                
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
                _waiting.Add(new AttackHandle(entry.weapon, runner, routine: null));
            }
        }
        
        // Create attack coroutine using handle, start it, and move to _active list
        private void StartAttack(AttackHandle handle, float duration)
        {
            // // Works but old way
            // var routine = handle.Weapon.Attack(duration, () =>
            // {
            //     handle.Complete();
            //     _active.Remove(handle);
            //     _waiting.Add(handle);
            // });
            handle.Start(duration, target, () =>
            {
                _active.Remove(handle);
                _waiting.Add(handle);
            });
            
            _active.Add(handle);
        }
        
        void FixedUpdate()
        {
            if (_weapons.Count == 0 || !target) return;

            _intervalTimer += Time.fixedDeltaTime;

            // Handle movement
            MoveTowardsTarget();
            
            // Handle Attacks
            if (_intervalTimer >= _nextInterval)
            {
                _intervalTimer = 0f;
                _nextInterval  = Random.Range(intervalMin, intervalMax);

                if (_waiting.Count > 0 && _active.Count < maxConcurrentSwings)
                {
                    if (_active.Count == 0 || Random.value < chanceForConcurrency)
                    {
                        int idx = Random.Range(0, _waiting.Count);
                        var handle = _waiting[idx];
                        _waiting.RemoveAt(idx);

                        float duration = GetInformedDuration();
                        StartAttack(handle, duration);
                    }
                }
            }
        }

        public float GetInformedDuration()
        {
            float progress = Mathf.Clamp01(_step / (float)(totalSteps - 1));
            float bucket = Mathf.Floor(progress / shiftStepPercent) * shiftStepPercent;
            float t = (rangeShiftCurve.Evaluate(bucket) + 1f) * 0.5f;
            return Mathf.Lerp(durationMin, durationMax, t);
        }

        private void MoveTowardsTarget()
        {
            if (disableMovement) return;

            var targetPosition = target.localPosition;

            foreach (var handle in _waiting)
            {
                if (handle.Weapon.CannotReach(targetPosition))
                {
                    handle.Weapon.ApproachTarget(targetPosition);
                } else {
                    handle.Weapon.StopApproaching();
                }
            }
        }
        
        
        // Might as well add some helpers
        private void CancelAndReturn(AttackHandle handle)
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
                if (entry.transform != null)
                    Destroy(entry.transform.gameObject);
            }

            CollectWeapons();
            ResetScheduler();
        }
    }
}
