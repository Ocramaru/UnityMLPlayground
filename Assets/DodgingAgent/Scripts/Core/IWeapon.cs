using System;
using UnityEngine;

namespace DodgyBall.Scripts.Core
{
    public interface IWeapon
    {
        Coroutine Attack(float duration, Vector3 targetPosition, Action onComplete);
        float GetImpactTime(float duration, Vector3 targetPosition); // Returns time of impact
    }
}