using System.Collections.Generic;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>首を横に振ったら成立（断る）。左右の往復を一定時間内に数える。</summary>
    public class HeadShakeTrigger : BranchTrigger
    {
        public float angleThreshold = 15f;
        public int swingsRequired = 3;
        public float window = 2.0f;

        float extremeYaw;
        int direction;
        readonly List<float> swingTimes = new();

        protected override void OnArmed()
        {
            extremeYaw = Head.eulerAngles.y;
            direction = 0;
            swingTimes.Clear();
        }

        void Update()
        {
            if (!Armed || Fired) return;
            float yaw = Head.eulerAngles.y;
            float delta = Mathf.DeltaAngle(extremeYaw, yaw);
            int dir = delta > 0 ? 1 : -1;

            if (direction == 0 || dir == direction)
            {
                // 同じ向きに回り続けている間は極値を更新
                if (direction == 0 && Mathf.Abs(delta) > angleThreshold) { direction = dir; extremeYaw = yaw; }
                else if (dir == direction) extremeYaw = yaw;
            }
            else if (Mathf.Abs(delta) > angleThreshold)
            {
                direction = dir;
                extremeYaw = yaw;
                swingTimes.Add(Time.time);
            }

            swingTimes.RemoveAll(t => Time.time - t > window);
            if (swingTimes.Count >= swingsRequired) Fire();
        }
    }
}
