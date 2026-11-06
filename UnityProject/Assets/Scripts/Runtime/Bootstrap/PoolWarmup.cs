using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Pool;

namespace BorrowedSouls.Bootstrap
{
    /// <summary>
    /// Fills object pools a little at a time, so a scene full of them doesn't spend one long frame on Instantiate. All pools share one budget
    /// per frame, because five pools with a budget each would still hold the frame for five budgets. A faster machine gets through more per
    /// frame, a slower one fewer. A coroutine only gives way where it yields; this is the code that decides when.
    /// </summary>
    public static class PoolWarmup
    {
        private const long BudgetMilliseconds = 4;

        private static readonly Stopwatch Clock = new Stopwatch();
        private static readonly List<Component> Working = new List<Component>();
        private static int _frame = -1;

        /// <summary>True while any pool in the live scene is still filling. A pool removed with its scene no longer counts.</summary>
        public static bool Busy
        {
            get
            {
                Working.RemoveAll(owner => owner == null);
                return Working.Count > 0;
            }
        }

        /// <summary>Marks the pool as filling at once, before the coroutine's first step. Run the result with the owner's StartCoroutine.</summary>
        public static IEnumerator Fill<T>(Component owner, IObjectPool<T> pool, int count) where T : Component
        {
            Working.Add(owner);
            return Steps(owner, pool, count);
        }

        private static IEnumerator Steps<T>(Component owner, IObjectPool<T> pool, int count) where T : Component
        {
            var held = new List<T>(count); // kept out of the pool until the end, or the next Get would hand back the same one
            for (int i = 0; i < count; i++)
            {
                while (BudgetSpent()) yield return null;
                T item = pool.Get();
                item.gameObject.SetActive(false);
                held.Add(item);
            }
            foreach (T item in held) pool.Release(item);
            Working.Remove(owner);
        }

        private static bool BudgetSpent()
        {
            if (Time.frameCount != _frame)
            {
                _frame = Time.frameCount;
                Clock.Restart();
                return false;
            }
            return Clock.ElapsedMilliseconds >= BudgetMilliseconds;
        }
    }
}
