using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting.Security
{
    /// <summary>
    /// Provides utilities for secure RPC.
    /// </summary>
    static class SecureUtils
    {
        private const int TimeslotDuration = 1800; // 30 minutes
        private const int TimeslotVariation = 180; // 3 minutes
        
        /// <summary>
        /// Gets the next time slot for next key.
        /// If the slot expires within 3 minutes it will return the next slot to maximise key time.
        /// </summary>
        /// <returns></returns>
        public static long GetNextTimeSlot() {
            // get current slot
            long currentSlot = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / TimeslotDuration;
            DateTimeOffset currentSlotTime = DateTimeOffset.FromUnixTimeSeconds(currentSlot * TimeslotDuration);

            // get the next slot
            long nextSlot = currentSlot + 1;
            DateTimeOffset nextSlotTime = DateTimeOffset.FromUnixTimeSeconds(currentSlot * TimeslotDuration);

            if (nextSlotTime - DateTimeOffset.UtcNow < TimeSpan.FromSeconds(TimeslotVariation))
                return nextSlot;
            else
                return currentSlot;
        }

        /// <summary>
        /// Gets if a time slot has expired.
        /// Time slots are active for 30 minutes, the next or previous slot can be used 3 minutes after or before.
        /// </summary>
        /// <param name="timeSlot">The time slot.</param>
        /// <returns></returns>
        public static bool HasTimeSlotExpired(long timeSlot) {
            // get time slot time
            DateTimeOffset timeSlotTime = DateTimeOffset.FromUnixTimeSeconds(timeSlot * TimeslotDuration);

            // check if it's too early within variation
            if (timeSlotTime - DateTimeOffset.UtcNow > TimeSpan.FromSeconds(TimeslotVariation))
                return false;

            // check if it's within expiry + variation
            if (timeSlotTime + TimeSpan.FromSeconds(TimeslotDuration) + TimeSpan.FromSeconds(TimeslotVariation) < DateTimeOffset.UtcNow)
                return false;

            return true;
        }
    }
}
