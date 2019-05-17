using Holon.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Holon
{
    /// <summary>
    /// Represents a rule for routing requests to transports.
    /// </summary>
    public abstract class RoutingRule
    {
        /// <summary>
        /// Executes this rule on the provided address.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <returns>The result.</returns>
        public abstract RoutingResult Execute(Address addr);
    }

    /// <summary>
    /// Implements a rule which matches a regex.
    /// </summary>
    public class RegexRule : RoutingRule
    {
        private Regex _regex;
        private Transport _transport;

        /// <summary>
        /// Executes this rule on the provided address.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <returns>The result.</returns>
        public override RoutingResult Execute(Address addr)
        {
            return new RoutingResult()
            {
                Matched = _regex.Match(addr.ToString()).Success,
                Transport = _transport
            };
        }

        /// <summary>
        /// Creates a new regex rule.
        /// </summary>
        /// <param name="regex">The regex.</param>
        /// <param name="transport">The transport.</param>
        public RegexRule(Regex regex, Transport transport)
        {
            _regex = regex;
            _transport = transport;
        }
    }

    /// <summary>
    /// Implements a rule which executes a delegate.
    /// </summary>
    public class FunctionRule : RoutingRule
    {
        private Func<Address, RoutingResult> _func;

        /// <summary>
        /// Executes this rule on the provided address.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <returns>The result.</returns>
        public override RoutingResult Execute(Address addr)
        {
            return _func(addr);
        }

        /// <summary>
        /// Creates a new function rule.
        /// </summary>
        /// <param name="func">The function.</param>
        public FunctionRule(Func<Address, RoutingResult> func)
        {
            _func = func;
        }
    }

    /// <summary>
    /// Implements a static routing rule.
    /// </summary>
    public class StaticRule : RoutingRule
    {
        private string _logicalNamespace;
        private string _physicalNamespace;
        private Transport _transport;

        /// <summary>
        /// Executes this rule on the provided address.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <returns>The result.</returns>
        public override RoutingResult Execute(Address addr)
        {
            // check if the namespace matches
            bool match = _logicalNamespace == "*" || _logicalNamespace.Equals(addr.Namespace, StringComparison.CurrentCultureIgnoreCase);

            // transform address
            ServiceAddress translatedAddress = null;

            if (match && _physicalNamespace != null)
                translatedAddress = new ServiceAddress(_physicalNamespace, addr.Key);

            // create the result
            return new RoutingResult()
            {
                Matched = match,
                TranslatedAddress = translatedAddress,
                Transport = _transport
            };
        }

        /// <summary>
        /// Creates a new static rule.
        /// </summary>
        /// <param name="logicalNamespace">The logical namespace.</param>
        /// <param name="transport">The transport.</param>
        /// <param name="physicalNamespace">The physical namespace.</param>
        public StaticRule(string logicalNamespace, Transport transport, string physicalNamespace)
        {
            _logicalNamespace = logicalNamespace;
            _physicalNamespace = physicalNamespace;
            _transport = transport;
        }

        /// <summary>
        /// Creates a new static rule.
        /// </summary>
        /// <param name="logicalNamespace">The logical namespace.</param>
        /// <param name="transport">The transport.</param>
        public StaticRule(string logicalNamespace, Transport transport)
            : this(logicalNamespace, transport, null) { }
    }
}
