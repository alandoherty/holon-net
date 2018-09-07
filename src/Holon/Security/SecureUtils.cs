using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Holon.Security
{
    /// <summary>
    /// Provides utilities for secure channel.
    /// </summary>
    static class SecureUtils
    {
        private const int TimeslotDuration = 1800; // 30 minutes
        private const int TimeslotVariation = 180; // 3 minutes

        /// <summary>
        /// Generate the AES128 key for the provided nonce and time slot.
        /// </summary>
        /// <param name="nonceBytes">The nonce bytes.</param>
        /// <param name="timeSlot">The time slot.</param>
        /// <param name="secret">The secret.</param>
        /// <returns>The key bytes.</returns>
        public static byte[] GenerateKey(byte[] nonceBytes, long timeSlot, byte[] secret) {
            // build input bytes
            byte[] inputBytes = new byte[nonceBytes.Length + 8];

            Buffer.BlockCopy(nonceBytes, 0, inputBytes, 0, nonceBytes.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(timeSlot), 0, inputBytes, nonceBytes.Length, 8);

            // get time slot
            byte[] keyBytes = new byte[16];

            using (HMACSHA256 hmac = new HMACSHA256(secret)) {
                byte[] hashBytes = hmac.ComputeHash(inputBytes);
                Buffer.BlockCopy(hashBytes, 0, keyBytes, 0, 16);
            }

            return keyBytes;
        }

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
            DateTimeOffset nextSlotTime = DateTimeOffset.FromUnixTimeSeconds(nextSlot * TimeslotDuration);

            if (nextSlotTime - DateTimeOffset.UtcNow < TimeSpan.FromSeconds(TimeslotVariation))
                return nextSlot;
            else
                return currentSlot;
        }

        /// <summary>
        /// Gets if a time slot has expired.
        /// Time slots are active for 30 minutes, the next or previous slot can be used 3 minutes after or before.
        /// This expiry function should allow variation on the behaviour and not on the proxy, this was the proxy always renews early.
        /// </summary>
        /// <param name="timeSlot">The time slot.</param>
        /// <param name="allowVariation">If expiry should allow the variation of 3 minutes.</param>
        /// <returns></returns>
        public static bool HasTimeSlotExpired(long timeSlot, bool allowVariation) {
            // get time slot time
            DateTimeOffset timeSlotTime = DateTimeOffset.FromUnixTimeSeconds(timeSlot * TimeslotDuration);

            // check if it's too early within variation
            if (timeSlotTime - DateTimeOffset.UtcNow > TimeSpan.FromSeconds(TimeslotVariation))
                return true;

            // check if it's within expiry + variation
            if (timeSlotTime + TimeSpan.FromSeconds(TimeslotDuration) 
                + (allowVariation ? TimeSpan.FromSeconds(TimeslotVariation) : TimeSpan.Zero) < DateTimeOffset.UtcNow)
                return true;

            return false;
        }
    }
}
