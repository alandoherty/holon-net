using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Holon.Metrics
{
    /// <summary>
    /// Provides a interface for metrics.
    /// </summary>
    public interface IMetric
    { 
        /// <summary>
        /// Gets the name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the value.
        /// </summary>
        object Value { get; }

        /// <summary>
        /// Gets the identifier.
        /// </summary>
        string Identifier { get; }

        /// <summary>
        /// Gets the default value.
        /// </summary>
        object DefaultValue { get; }

        /// <summary>
        /// Submits the value with the current UTC time.
        /// </summary>
        /// <param name="val">The value.</param>
        void Submit(object val);

        /// <summary>
        /// Submits the value with the provided time.
        /// </summary>
        /// <param name="dateTime">The date/time.</param>
        /// <param name="val">The value.</param>
        void Submit(DateTime dateTime, object val);
    }

    /// <summary>
    /// Represents a metric type.
    /// </summary>
    /// <typeparam name="TValue">The metric value type.</typeparam>
    public class Metric<TValue> : IMetric where TValue : struct
    {
        #region Fields
        private MetricValue<TValue> _value = default(MetricValue<TValue>);
        private string _name;
        private string _identifier;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the last submitted value.
        /// </summary>
        public MetricValue<TValue> Value {
            get {
                return _value;
            }
        }

        /// <summary>
        /// Gets the last submitted value.
        /// </summary>
        object IMetric.Value {
            get {
                return _value.Value;
            }
        }

        /// <summary>
        /// Gets the name of the metric.
        /// </summary>
        public string Name {
            get {
                return _name;
            }
        }

        /// <summary>
        /// Gets the type of the value.
        /// </summary>
        public Type ValueType {
            get {
                return typeof(TValue);
            }
        }

        /// <summary>
        /// Gets the metric identifier.
        /// </summary>
        public string Identifier {
            get {
                return _identifier;
            }
        }

        /// <summary>
        /// Gets the default value.
        /// </summary>
        object IMetric.DefaultValue {
            get {
                return default(TValue);
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Clears all metric data.
        /// </summary>
        public void Clear() {
            _value = default(MetricValue<TValue>);
        }

        /// <summary>
        /// Submits a value.
        /// </summary>
        /// <param name="val">The value.</param>
        public void Submit(TValue val) {
            Submit(new MetricValue<TValue>(val));
        }

        /// <summary>
        /// Submits a metric value.
        /// </summary>
        /// <param name="metricVal">The metric value.</param>
        public void Submit(MetricValue<TValue> metricVal) {
            _value = metricVal;
        }

        void IMetric.Submit(object val) {
            Submit((TValue)val);
        }

        void IMetric.Submit(DateTime dateTime, object val) {
            Submit(new MetricValue<TValue>(dateTime, (TValue)val));
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a trackable metric.
        /// </summary>
        /// <param name="identifier">The identifier.</param>
        /// <param name="name">The name.</param>
        public Metric(string identifier, string name) {
            _identifier = identifier;
            _name = name;
        }
        #endregion
    }
}
