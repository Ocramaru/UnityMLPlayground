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
        Vector3 GetRandomInRangePosition(Transform target);
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
        
        [Header("Target")] 
        public Transform target;
        
        private readonly List<WeaponEntry> _weapons = new();
        private readonly List<AttackHandle> _waiting = new();
        private readonly List<AttackHandle> _active = new();
        
        private Coroutine _scheduler;
        
        // Cache thy updates ;)
        private static readonly WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();

        void Awake()
        {
            if (!target) { Debug.LogWarning("Orchestrator: no target assigned."); return; }
            
            CollectWeapons();
        }
        
        void OnEnable()
        {
            if (_weapons.Count == 0) return;
            _scheduler = StartCoroutine(SchedulerLoop());
        }
        void OnDisable()
        {
            if (_scheduler != null) StopCoroutine(_scheduler);
            StopAllAttacks();
        }
        
        private void CollectWeapons()
        {
            _weapons.Clear();
            if (weaponPrefabs == null || weaponPrefabs.Length == 0)
            {
                Debug.LogWarning("No weaponPrefabs assigned.");
                return;
            }
            
            foreach (var prefab in weaponPrefabs)
            {
                if (!prefab) continue;
                
                GameObject initialized = Instantiate(prefab, transform);
                
                IWeapon iWeapon = initialized.GetComponent<IWeapon>() ?? initialized.GetComponentInChildren<IWeapon>(true);
                if (iWeapon == null) { Debug.LogError($"Prefab '{prefab.name}' has no component implementing IWeapon."); continue; }
                initialized.transform.position = iWeapon.GetRandomInRangePosition(target);
                
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
            handle.Start(duration, () =>
            {
                _active.Remove(handle);
                _waiting.Add(handle);
            });
            
            _active.Add(handle);
        }
        
        private IEnumerator SchedulerLoop()
        {
            while (true)
            {
                if (_waiting.Count > 0 && _active.Count < maxConcurrentSwings)
                {
                    bool allowNew = _active.Count == 0 || Random.value < chanceForConcurrency;
                    if (allowNew)
                    {
                        // pick a random waiting weapon
                        int idx = Random.Range(0, _waiting.Count);
                        var handle = _waiting[idx];
                        _waiting.RemoveAt(idx);

                        float dur = GetInformedDuration();
                        StartAttack(handle, dur);
                    }
                }

                float wait = Random.Range(intervalMin, intervalMax);
                yield return FixedWait(wait);
            }
        }
        
        // Simulate wait time in seconds using delta time
        private static IEnumerator FixedWait(float seconds)
        {
            float remaining = seconds;
            while (remaining > 0f)
            {
                yield return waitForFixedUpdate;
                remaining -= Time.fixedDeltaTime;
            }
        }

        public float GetInformedDuration()
        {
            float progress = Mathf.Clamp01(_step / (float)(totalSteps - 1));
            float bucket = Mathf.Floor(progress / shiftStepPercent) * shiftStepPercent;
            float t = (rangeShiftCurve.Evaluate(bucket) + 1f) * 0.5f;
            return Mathf.Lerp(durationMin, durationMax, t);
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
    }
}
