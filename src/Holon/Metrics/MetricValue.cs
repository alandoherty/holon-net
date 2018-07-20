using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Metrics
{
    /// <summary>
    /// Provides an interface for a metric value in time.
    /// </summary>
    public interface IMetricValue
    {
        /// <summary>
        /// Gets the datetime the value was retrieved at.
        /// </summary>
        DateTime DateTime { get; }

        /// <summary>
        /// Gets the metric value.
        /// </summary>
        object Value { get; }
    }

    /// <summary>
    /// Represents a metric value in time.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    public struct MetricValue<TValue> : IMetricValue
    {
        #region Fields
        private DateTime _dateTime;
        private TValue _value;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the date time for the current timezone.
        /// </summary>
        public DateTime DateTime {
            get {
                return _dateTime;
            }
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public TValue Value {
            get {
                return _value;
            }
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        object IMetricValue.Value {
            get {
                return _value;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Gets the string representation of the metric value.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return string.Format("{0} ({1})", Value, DateTime);
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a metric value.
        /// </summary>
        /// <param name="dateTime">The date time.</param>
        /// <param name="value">The value.</param>
        public MetricValue(DateTime dateTime, TValue value) {
            _value = value;
            _dateTime = dateTime;
        }

        /// <summary>
        /// Creates a metric value at the current time.
        /// </summary>
        /// <param name="value">The value.</param>
        public MetricValue(TValue value)
            : this(DateTime.UtcNow, value) {
        }
        #endregion
    }
}
