﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Events
{
    /// <summary>
    /// Represents an event address.
    /// </summary>
    public class EventAddress : IEquatable<EventAddress>
    {
        #region Fields
        private int _namespaceLength;
        private int _resourceIndex;
        private int _resourceLength;
        private int _nameIndex;
        private int _nameLength;

        private string _addr;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the namespace.
        /// </summary>
        public string Namespace {
            get {
                return _addr.Substring(0, _namespaceLength);
            }
        }

        /// <summary>
        /// Gets the resource.
        /// </summary>
        public string Resource {
            get {
                return _addr.Substring(_resourceIndex, _resourceLength);
            }
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name {
            get {
                return _addr.Substring(_nameIndex, _nameLength);
            }
        }
        #endregion

        #region Methods
        private bool InternalTryParse(string addr) {
            _addr = addr;
            int state = 0;

            for (int i = 0; i < addr.Length; i++) {
                if (state == 0) {
                    if (addr[i] == ':') {
                        state = 1;
                        _namespaceLength = i;
                        _resourceIndex = i + 1;
                    }
                } else if (state == 1) { 
                    if (addr[i] == '.') {
                        _nameIndex = i + 1;
                        _resourceLength = (i - _resourceIndex);
                    }
                }
            }

            _nameLength = addr.Length - _nameIndex;

            // check if the end state was correct
            if (state < 1)
                return false;

            // check if the namespace is longer than one character
            if (_namespaceLength < 1)
                return false;

            // check if the resource is longer than one character
            if (_resourceLength < 1)
                return false;

            // check if the name is longer than one character
            if (_nameLength < 1)
                return false;

            return true;
        }

        /// <summary>
        /// Trys to parse the provided event address.
        /// </summary>
        /// <param name="addr">The address string.</param>
        /// <param name="eventAddr">The output event address.</param>
        /// <returns></returns>
        public static bool TryParse(string addr, out EventAddress eventAddr) {
            eventAddr = new EventAddress();
            return eventAddr.InternalTryParse(addr);
        }

        /// <summary>
        /// Parses the provided event address.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <exception cref="FormatException">The format is invalid.</exception>
        /// <returns></returns>
        public static EventAddress Parse(string addr) {
            if (!TryParse(addr, out EventAddress servAddr))
                throw new FormatException("The event address format is invalid");

            return servAddr;
        }

        /// <summary>
        /// Gets the string representation of this event address.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return _addr;
        }

        /// <summary>
        /// Gets the hash code of this event address.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() {
            return _addr.GetHashCode();
        }

        /// <summary>
        /// Compares two objects for equality.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns></returns>
        public override bool Equals(object obj) {
            if (obj is EventAddress)
                return Equals((EventAddress)obj);
            else
                return false;
        }

        /// <summary>
        /// Compares two event addresses for equality.
        /// </summary>
        /// <param name="other">The other address.</param>
        /// <returns></returns>
        public bool Equals(EventAddress other) {
            return _addr == other._addr;
        }
        #endregion

        #region Constructors
        private EventAddress() { }

        /// <summary>
        /// Creates a new event address with the provided string representation.
        /// </summary>
        /// <param name="addr">The event address.</param>
        public EventAddress(string addr) {
            if (!InternalTryParse(addr))
                throw new FormatException("The event address format is invalid");
        }

        /// <summary>
        /// Creates a new event address with the provided string representation.
        /// </summary>
        /// <param name="namespace">The namespace.</param>
        /// <param name="resource">The resource name..</param>
        /// <param name="name">The event name.</param>
        public EventAddress(string @namespace, string resource, string name) {
            if (!InternalTryParse($"{@namespace}:{resource}.{name}"))
                throw new FormatException("The event address format is invalid");
        }
        #endregion
    }
}
