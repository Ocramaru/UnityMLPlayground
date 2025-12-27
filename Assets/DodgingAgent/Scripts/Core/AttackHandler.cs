using System;
using UnityEngine;

namespace DodgingAgent.Scripts.Core
{
    public sealed class AttackHandler
    {
        public IWeapon Weapon { get; }
        public MonoBehaviour Runner { get; }
        public Coroutine Routine { get; private set; }
        public bool IsActive => Routine != null;

        public AttackHandler(IWeapon weapon, MonoBehaviour runner, Coroutine routine)
        {
            Weapon = weapon;
            Runner = runner;
            Routine = routine;
        }
        
        public void Start(Coroutine routine) => Routine = routine;
        public void Start(float duration, Vector3 targetPosition, Action onFinished)
        {
            Routine = Weapon.Attack(duration, targetPosition, () =>
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
}